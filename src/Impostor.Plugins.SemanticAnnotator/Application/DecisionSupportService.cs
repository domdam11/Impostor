using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Jobs;
using Impostor.Plugins.SemanticAnnotator.Models.Options;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Plugins.SemanticAnnotator.Application
{
    public class DecisionSupportService : IDecisionSupportService
    {

        private readonly IAnnotator _annotator;
        private readonly IArgumentationService _argumentation;
        private readonly ISemanticEventRecorder _notarization;
        private readonly ILogger<DecisionSupportService> _logger;
        private readonly bool _notarizationEnabled;
        private readonly bool _argumentationEnabled;
        private readonly GameEventCacheManager _cacheManager;

        // Campi statici
        private static readonly Meter Meter = new("SemanticAnnotator.DSS");

        // Histogram
        private static readonly Histogram<double> ProcessDuration = Meter.CreateHistogram<double>("dss_total_duration_ms", "ms");
        private static readonly Histogram<double> AnnotationDuration = Meter.CreateHistogram<double>("dss_annotation_duration_ms", "ms");
        private static readonly Histogram<double> ArgumentationDuration = Meter.CreateHistogram<double>("dss_argumentation_duration_ms", "ms");
        private static readonly Histogram<double> NotarizationDuration = Meter.CreateHistogram<double>("dss_notarization_duration_ms", "ms");

        // Gauge support: min/max
        private static double _annotationMin = double.MaxValue, _annotationMax = double.MinValue;
        private static double _argumentationMin = double.MaxValue, _argumentationMax = double.MinValue;
        private static double _notarizationMin = double.MaxValue, _notarizationMax = double.MinValue;
        private static double _processMin = double.MaxValue, _processMax = double.MinValue;

        // Gauge registration
        static DecisionSupportService()
        {
            Meter.CreateObservableGauge("dss_annotation_duration_min_ms", () => _annotationMin);
            Meter.CreateObservableGauge("dss_annotation_duration_max_ms", () => _annotationMax);
            Meter.CreateObservableGauge("dss_argumentation_duration_min_ms", () => _argumentationMin);
            Meter.CreateObservableGauge("dss_argumentation_duration_max_ms", () => _argumentationMax);
            Meter.CreateObservableGauge("dss_notarization_duration_min_ms", () => _notarizationMin);
            Meter.CreateObservableGauge("dss_notarization_duration_max_ms", () => _notarizationMax);
            Meter.CreateObservableGauge("dss_total_duration_min_ms", () => _processMin);
            Meter.CreateObservableGauge("dss_total_duration_max_ms", () => _processMax);
        }

        private readonly KeyedTaskQueue _queue;

        public DecisionSupportService(IAnnotator annotator,
                                      IArgumentationService argumentation,
                                      ISemanticEventRecorder notarization,
                                      ILogger<DecisionSupportService> logger, IOptions<ArgumentationServiceOptions> argumentationOptions, IOptions<NotarizationServiceOptions> notarizationOptions, GameEventCacheManager cacheManager, KeyedTaskQueue queue)
        {
            _annotator = annotator;
            _argumentation = argumentation;
            _notarization = notarization;
            _logger = logger;
            _notarizationEnabled = notarizationOptions.Value.Enabled;
            _argumentationEnabled = argumentationOptions.Value.Enabled;
            _cacheManager = cacheManager;
            _queue = queue;
        }

        public async Task ProcessAsync(string gameCode)
        {

            var swTotal = Stopwatch.StartNew();
            var annotationKey = _cacheManager.GetGameSessionUniqueId(gameCode);
            var isInMatch = _cacheManager.IsInMatch(gameCode);
            var annotationEventId = _cacheManager.GetAnnotationEventId(gameCode);
            var globalAssetKey = _cacheManager.GetGameSessionUniqueId(gameCode);
            var players = _cacheManager.GetPlayerList(gameCode);
            var events = _cacheManager.GetEventsOnlyNotarizedByGameCodeAsync(gameCode);

            var eventsPerAssetKey = new Dictionary<string, List<IGameEvent>>();

            foreach (var gameEvent in events)
            {
                if (gameEvent is IGameStartedEvent started)
                {
                    _cacheManager.StartGame(started.Game);

                    var assetKey = _cacheManager.GetGameSessionUniqueId(started.Game.Code);
                    if (!eventsPerAssetKey.TryGetValue(assetKey, out var list))
                    {
                        list = new List<IGameEvent>();
                        eventsPerAssetKey[assetKey] = list;
                    }
                    list.Add(started);
                }
                else if (gameEvent is IGameEndedEvent ended)
                {
                    _cacheManager.CheckEndGame(ended.Game, _annotator);

                    var assetKey = _cacheManager.GetGameSessionUniqueId(ended.Game.Code);
                    if (!eventsPerAssetKey.TryGetValue(assetKey, out var list))
                    {
                        list = new List<IGameEvent>();
                        eventsPerAssetKey[assetKey] = list;
                    }
                    list.Add(ended);
                }
            }
            _cacheManager.ClearEventsOnlyNotarizedByGameCodeAsync(gameCode);

            if (_notarizationEnabled)
            {
                if (events.Any())
                {
                    foreach (var kvp in eventsPerAssetKey)
                    {
                        var assetKey = kvp.Key;
                        var eventList = kvp.Value;

                        await _queue.EnqueueAsync(assetKey, async () =>
                        {
                            var swNot = Stopwatch.StartNew();

                            try
                            {
                                var listEvents = await _notarization.StoreGameEventsAsync(gameCode, assetKey, eventList, players);
                                swNot.Stop();

                                if (listEvents.Any())
                                {
                                    var notMs = swNot.Elapsed.TotalMilliseconds;
                                    TemporalTraceCollector.Log(gameCode, assetKey, TracePhase.NotarizationOnly, notMs, null);
                                    NotarizationDuration.Record(notMs);
                                    _notarizationMin = Math.Min(_notarizationMin, notMs);
                                    _notarizationMax = Math.Max(_notarizationMax, notMs);
                                    _logger.LogInformation("[DSS::NOTARIZATION ONLY] {GameCode} - Duration: {Duration}ms", gameCode, notMs);
                                }
                                else
                                {
                                    _logger.LogDebug("[DSS::NOTARIZATION ONLY] {GameCode} - Nessun evento da processare", gameCode);
                                }
                            }
                            catch (Exception ex)
                            {
                                swNot.Stop();
                                _logger.LogError(ex, "[DSS::NOTARIZATION ONLY] Errore nella notarizzazione asincrona per {GameCode}", gameCode);
                            }
                        });
                    }
                }
            }
            if (isInMatch)
            {
                var swAnnot = Stopwatch.StartNew();

                var annotationData = await _annotator.AnnotateAsync(gameCode);
                
                swAnnot.Stop();
                var annotMs = swAnnot.Elapsed.TotalMilliseconds;
                TemporalTraceCollector.Log(annotationKey, annotationEventId, TracePhase.Annotation, annotMs, annotationData);
                AnnotationDuration.Record(annotMs);
                _annotationMin = Math.Min(_annotationMin, annotMs);
                _annotationMax = Math.Max(_annotationMax, annotMs);
                _logger.LogInformation("[DSS::ANNOTATION] {GameCode} - Duration: {Duration}ms", annotationKey, annotMs);
                
                string result = null;

                if (!annotationData.IsEmpty())
                {
                    if (_argumentationEnabled)
                    {
                        // Accoda argumentation
                        await _queue.EnqueueAsync("argumentation", async () =>
                        {
                            var swArg = Stopwatch.StartNew();
                            string result = null;

                            try
                            {
                                result = await _argumentation.SendAnnotationsAsync(annotationData.OwlDescription);
                                swArg.Stop();
                                _cacheManager.SetLastStrategy(gameCode, result);
                                var argMs = swArg.Elapsed.TotalMilliseconds;
                                TemporalTraceCollector.Log(annotationKey, annotationEventId, TracePhase.Argumentation, argMs, annotationData);
                                ArgumentationDuration.Record(argMs);
                                _argumentationMin = Math.Min(_argumentationMin, argMs);
                                _argumentationMax = Math.Max(_argumentationMax, argMs);
                                _logger.LogInformation("[DSS::ARGUMENTATION] {GameCode} - Duration: {Duration}ms", annotationKey, argMs);
                            }
                            catch (Exception ex)
                            {
                                swArg.Stop();
                                _logger.LogError(ex, "[DSS::ARGUMENTATION] Errore per {GameCode}", annotationKey);
                            }

                            // 🔁 Avvia la notarizzazione come secondo task separato, accodato dopo
                            if (_notarizationEnabled && !string.IsNullOrWhiteSpace(result))
                            {
                                await _queue.EnqueueAsync(globalAssetKey, async () =>
                                {
                                    var swNot = Stopwatch.StartNew();
                                    try
                                    {
                                        await _notarization.StoreAnnotationAsync(annotationKey, annotationEventId, annotationData.OwlDescription, result);
                                        swNot.Stop();
                                        var notMs = swNot.Elapsed.TotalMilliseconds;
                                        TemporalTraceCollector.Log(annotationKey, annotationEventId, TracePhase.Notarization, notMs, annotationData);
                                        NotarizationDuration.Record(notMs);
                                        _notarizationMin = Math.Min(_notarizationMin, notMs);
                                        _notarizationMax = Math.Max(_notarizationMax, notMs);
                                        _logger.LogInformation("[DSS::NOTARIZATION] {GameCode} - Duration: {Duration}ms", annotationKey, notMs);
                                    }
                                    catch (Exception ex)
                                    {
                                        swNot.Stop();
                                        _logger.LogError(ex, "[DSS::NOTARIZATION] Errore durante NotifyAsync per {GameCode}", annotationKey);
                                    }
                                });
                            }
                        });
                    }
                }
                else
                {
                    _logger.LogWarning("[DSS::ANNOTATION] {GameCode} - Annotazione non generata", annotationKey);
                }
                swTotal.Stop();
                var totalMs = swTotal.Elapsed.TotalMilliseconds;
                ProcessDuration.Record(totalMs);
                _processMin = Math.Min(_processMin, totalMs);
                _processMax = Math.Max(_processMax, totalMs);
                _logger.LogInformation("[DSS::TOTAL] {GameCode} - Total Duration: {Duration}ms", annotationKey, totalMs);
                Console.WriteLine(result);
            }
        }

        public async Task ProcessMultipleAsync(IEnumerable<string> gameCodes)
        {
            _logger.LogInformation("Invoked Decision Support: active gamecodes ({0})", gameCodes.FirstOrDefault());
            foreach (var gameCode in gameCodes)
            {
                await ProcessAsync(gameCode);
            }
        }

    }
}
