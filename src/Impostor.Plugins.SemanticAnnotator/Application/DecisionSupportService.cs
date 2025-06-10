using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Threading.Tasks;
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

        public DecisionSupportService(IAnnotator annotator,
                                      IArgumentationService argumentation,
                                      INotarizationService notarization,
                                      ILogger<DecisionSupportService> logger, IOptions<ArgumentationServiceOptions> argumentationOptions, IOptions<NotarizationServiceOptions> notarizationOptions, GameEventCacheManager cacheManager)
        {
            _annotator = annotator;
            _argumentation = argumentation;
            _notarization = notarization;
            _logger = logger;
            _notarizationEnabled = notarizationOptions.Value.Enabled;
            _argumentationEnabled = argumentationOptions.Value.Enabled;
            _cacheManager = cacheManager;
        }

        public async Task ProcessAsync(string gameCode)
        {
            var swTotal = Stopwatch.StartNew();
            var annotationKey = _cacheManager.GetGameSessionUniqueId(gameCode);
            var isInMatch = _cacheManager.IsInMatch(gameCode);
            if (isInMatch)
            {
                var swAnnot = Stopwatch.StartNew();
                string owl = await _annotator.AnnotateAsync(gameCode);

                swAnnot.Stop();
                double annotMs = swAnnot.Elapsed.TotalMilliseconds;
                AnnotationDuration.Record(annotMs);
                _annotationMin = Math.Min(_annotationMin, annotMs);
                _annotationMax = Math.Max(_annotationMax, annotMs);
                _logger.LogInformation("[DSS::ANNOTATION] {GameCode} - Duration: {Duration}ms", annotationKey, annotMs);

                string result = null;

                if (!string.IsNullOrWhiteSpace(owl))
                {
                    if (_argumentationEnabled)
                    {
                        var swArg = Stopwatch.StartNew();
                        result = await _argumentation.SendAnnotationsAsync(owl);
                        swArg.Stop();
                        double argMs = swArg.Elapsed.TotalMilliseconds;
                        ArgumentationDuration.Record(argMs);
                        _argumentationMin = Math.Min(_argumentationMin, argMs);
                        _argumentationMax = Math.Max(_argumentationMax, argMs);
                        _logger.LogInformation("[DSS::ARGUMENTATION] {GameCode} - Duration: {Duration}ms", annotationKey, argMs);

                        if (_notarizationEnabled)
                        {
                            var swNot = Stopwatch.StartNew();
                            await _notarization.NotifyAsync(annotationKey, _cacheManager.GetAnnotationEventId(gameCode), owl, result);
                            swNot.Stop();
                            double notMs = swNot.Elapsed.TotalMilliseconds;
                            NotarizationDuration.Record(notMs);
                            _notarizationMin = Math.Min(_notarizationMin, notMs);
                            _notarizationMax = Math.Max(_notarizationMax, notMs);
                            _logger.LogInformation("[DSS::NOTARIZATION] {GameCode} - Duration: {Duration}ms", annotationKey, notMs);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning("[DSS::ANNOTATION] {GameCode} - Annotazione non generata", annotationKey);
                }
                swTotal.Stop();
                double totalMs = swTotal.Elapsed.TotalMilliseconds;
                ProcessDuration.Record(totalMs);
                _processMin = Math.Min(_processMin, totalMs);
                _processMax = Math.Max(_processMax, totalMs);
                _logger.LogInformation("[DSS::TOTAL] {GameCode} - Total Duration: {Duration}ms", annotationKey, totalMs);
                Console.WriteLine(result);
            }
            else
            {
                if (_notarizationEnabled)
                {
                    var swNot = Stopwatch.StartNew();
                    _logger.LogInformation("[DSS] Solo notarizzazione per {GameCode}...", annotationKey);
                    await _notarization.DispatchNotarizationTasksAsync();
                    swNot.Stop();
                    double notMs = swNot.Elapsed.TotalMilliseconds;
                    NotarizationDuration.Record(notMs);
                    _notarizationMin = Math.Min(_notarizationMin, notMs);
                    _notarizationMax = Math.Max(_notarizationMax, notMs);
                    _logger.LogInformation("[DSS::NOTARIZATION ONLY] {GameCode} - Duration: {Duration}ms", gameCode, notMs);
                }
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
}
