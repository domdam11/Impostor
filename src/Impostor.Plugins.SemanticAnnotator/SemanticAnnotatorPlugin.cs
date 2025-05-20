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
    [ImpostorPlugin("gg.impostor.semanticannotator")]
    public class SemanticAnnotatorPlugin : PluginBase
    {
        private readonly ILogger<SemanticAnnotatorPlugin> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;
        private readonly bool _useBuffer;
        private readonly int _annotationIntervalMilliseconds;

        public SemanticAnnotatorPlugin(ILogger<SemanticAnnotatorPlugin> logger, IServiceProvider serviceProvider, IConfiguration _configuration, IOptions<AnnotatorServiceOptions> options)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _useBuffer = _configuration.GetValue<bool>("UseBufferMode", true);
            _annotationIntervalMilliseconds = options.Value.AnnotationIntervalMilliseconds;
        }

        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("[SemanticAnnotatorPlugin] EnableAsync called.");

        

            var scheduler = _serviceProvider.GetRequiredService<IScheduler>();
            bool useBuffer = _configuration.GetValue<bool>("UseBufferMode", true);

            if (useBuffer)
            {
                scheduler.Schedule<AnnotationJob>()
                .EverySeconds(_annotationIntervalMilliseconds)
                .PreventOverlapping(nameof(AnnotationJob));

                scheduler.Schedule<ArgumentationJob>()
                    .EverySeconds(_annotationIntervalMilliseconds)
                    .PreventOverlapping(nameof(ArgumentationJob));

                scheduler.Schedule<GameNotarizationJob>()
                    .EverySeconds(_annotationIntervalMilliseconds)
                    .PreventOverlapping(nameof(GameNotarizationJob));
            }
            else
            {
                scheduler.Schedule<DecisionSupportJob>()
                    .EverySeconds(_annotationIntervalMilliseconds)
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
