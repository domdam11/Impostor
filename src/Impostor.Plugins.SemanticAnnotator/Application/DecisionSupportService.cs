using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using Coravel.Queuing.Interfaces;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Plugins.SemanticAnnotator.Application
{
    public class DecisionSupportService : IDecisionSupportService
    {
        private readonly IAnnotator _annotator;
        private readonly IArgumentationService _argumentation;
        private readonly INotarizationService _notarization;
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

        private readonly PerKeyTaskQueue _queue;

        public DecisionSupportService(IAnnotator annotator,
                                      IArgumentationService argumentation,
                                      INotarizationService notarization,
                                      ILogger<DecisionSupportService> logger, IOptions<ArgumentationServiceOptions> argumentationOptions, IOptions<NotarizationServiceOptions> notarizationOptions, GameEventCacheManager cacheManager, PerKeyTaskQueue queue)
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
            var assetKey = _cacheManager.GetGameSessionUniqueId(gameCode);
            var players = _cacheManager.GetPlayerList(gameCode);
            if (_notarizationEnabled)
            {
                var events = _cacheManager.GetEventsOnlyNotarizedByGameCodeAsync(gameCode);
                _cacheManager.ClearEventsOnlyNotarizedByGameCodeAsync(gameCode);

                if (events.Any())
                {
                    await _queue.EnqueueAsync(assetKey, async () =>
                    {
                        var swNot = Stopwatch.StartNew();

                        try
                        {

                            var listEvents = await _notarization.DispatchNotarizationTasksAsync(gameCode, assetKey, events, players);
                            swNot.Stop();

                            if (listEvents.Any())
                            {
                                var notMs = swNot.Elapsed.TotalMilliseconds;
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
            if (isInMatch)
            {
                var swAnnot = Stopwatch.StartNew();
                var owl = await _annotator.AnnotateAsync(gameCode);

                swAnnot.Stop();
                var annotMs = swAnnot.Elapsed.TotalMilliseconds;
                AnnotationDuration.Record(annotMs);
                _annotationMin = Math.Min(_annotationMin, annotMs);
                _annotationMax = Math.Max(_annotationMax, annotMs);
                _logger.LogInformation("[DSS::ANNOTATION] {GameCode} - Duration: {Duration}ms", annotationKey, annotMs);

                string result = null;

                if (!string.IsNullOrWhiteSpace(owl))
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
                                result = await _argumentation.SendAnnotationsAsync(owl);
                                swArg.Stop();

                                var argMs = swArg.Elapsed.TotalMilliseconds;
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
                                await _queue.EnqueueAsync(assetKey, async () =>
                                {
                                    var swNot = Stopwatch.StartNew();
                                    try
                                    {
                                        await _notarization.NotifyAsync(annotationKey, annotationEventId, owl, result);
                                        swNot.Stop();
                                        var notMs = swNot.Elapsed.TotalMilliseconds;
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
            foreach (var gameCode in gameCodes)
            {
                await ProcessAsync(gameCode);
            }
        }
    }
    public class PerKeyTaskQueue
    {
        private class TaskQueue
        {
            private readonly Channel<Func<Task>> _channel;
            public ChannelWriter<Func<Task>> Writer => _channel.Writer;
            public Task ProcessingTask { get; }

            private int _pendingTasks = 0;

            public TaskQueue()
            {
                _channel = Channel.CreateUnbounded<Func<Task>>();
                ProcessingTask = Task.Run(ProcessQueueAsync);
            }

            private async Task ProcessQueueAsync()
            {
                await foreach (var task in _channel.Reader.ReadAllAsync())
                {
                    Interlocked.Increment(ref _pendingTasks);
                    try
                    {
                        await task();
                    }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"Error in queued task: {ex}");
                    }
                    finally
                    {
                        Interlocked.Decrement(ref _pendingTasks);
                    }

                    await Task.Delay(50); // attesa tra task
                }
            }

            public async Task<bool> IsDrainedAsync(CancellationToken cancellationToken = default)
            {
                while (_pendingTasks > 0)
                {
                    await Task.Delay(25, cancellationToken);
                }

                return true;
            }
        }

        private readonly ConcurrentDictionary<string, TaskQueue> _queues = new();

        public async Task EnqueueAsync(string assetKey, Func<Task> task)
        {
            var queue = _queues.GetOrAdd(assetKey, _ => new TaskQueue());
            await queue.Writer.WriteAsync(task);
        }

        public async Task WaitUntilQueueIsDrainedAsync(string assetKey, CancellationToken cancellationToken = default)
        {
            if (_queues.TryGetValue(assetKey, out var queue))
            {
                await queue.IsDrainedAsync(cancellationToken);
            }
        }

        public async Task WaitUntilAllQueuesAreDrainedAsync(CancellationToken cancellationToken = default)
        {
            while (true)
            {
                bool allDrained = true;

                foreach (var kvp in _queues)
                {
                    if (!await kvp.Value.IsDrainedAsync(cancellationToken))
                    {
                        allDrained = false;
                        break;
                    }
                }

                if (allDrained || cancellationToken.IsCancellationRequested)
                    break;

                await Task.Delay(50, cancellationToken);
            }
        }

        public void CleanupDrainedQueues()
        {
            foreach (var kvp in _queues)
            {
                var key = kvp.Key;
                var queue = kvp.Value;

                if (queue.IsDrainedAsync().GetAwaiter().GetResult())
                {
                    _queues.TryRemove(key, out _);
                }
            }
        }
    }


}
