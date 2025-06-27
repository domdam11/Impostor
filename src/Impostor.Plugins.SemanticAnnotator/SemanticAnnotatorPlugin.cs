using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Coravel.Scheduling.Schedule.Interfaces;
using Impostor.Plugins.SemanticAnnotator.Jobs;
using Microsoft.Extensions.Configuration;
using Impostor.Plugins.SemanticAnnotator.Models;
using Microsoft.Extensions.Options;

namespace Impostor.Plugins.SemanticAnnotator
{
    [ImpostorPlugin("impostor.plugins.semanticannotator")]
    public class SemanticAnnotatorPlugin : PluginBase
    {
        private readonly ILogger<SemanticAnnotatorPlugin> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly bool _useBuffer;
        private readonly int _annotationIntervalMs;

        public SemanticAnnotatorPlugin(ILogger<SemanticAnnotatorPlugin> logger, IServiceProvider serviceProvider, IConfiguration _configuration, IOptions<SemanticPluginOptions> options)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _useBuffer = options.Value.UseBuffer;
            _annotationIntervalMs = options.Value.AnnotationIntervalMs;
        }

        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("[SemanticAnnotatorPlugin] EnableAsync called.");

            var scheduler = _serviceProvider.GetRequiredService<IScheduler>();

            if (_useBuffer)
            {
                scheduler.Schedule<AnnotationJob>()
                .EverySeconds(_annotationIntervalMs)
                .PreventOverlapping(nameof(AnnotationJob));

                scheduler.Schedule<ArgumentationJob>()
                    .EverySeconds(_annotationIntervalMs)
                    .PreventOverlapping(nameof(ArgumentationJob));

                scheduler.Schedule<GameNotarizationJob>()
                    .EverySeconds(_annotationIntervalMs)
                    .PreventOverlapping(nameof(GameNotarizationJob));
            }
            else
            {
                scheduler.Schedule<DecisionSupportJob>()
                    .EverySeconds(_annotationIntervalMs)
                    .PreventOverlapping(nameof(DecisionSupportJob));
            }


            return default;
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogInformation("[SemanticAnnotatorPlugin] DisableAsync called.");
            return default;
        }
    }
}
