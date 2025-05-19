using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Threading.Tasks;
using Coravel.Scheduling.Schedule.Interfaces;
using Impostor.Plugins.SemanticAnnotator.Jobs;
using Microsoft.Extensions.Configuration;

namespace Impostor.Plugins.SemanticAnnotator
{
    [ImpostorPlugin("gg.impostor.semanticannotator")]
    public class SemanticAnnotatorPlugin : PluginBase
    {
        private readonly ILogger<SemanticAnnotatorPlugin> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly IConfiguration _configuration;

        public SemanticAnnotatorPlugin(ILogger<SemanticAnnotatorPlugin> logger, IServiceProvider serviceProvider, IConfiguration configuration)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
        }

        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("[SemanticAnnotatorPlugin] EnableAsync called.");

            int annotationPeriod = int.Parse(Environment.GetEnvironmentVariable("ANNOTATION_PERIOD") ?? "30");

            var scheduler = _serviceProvider.GetRequiredService<IScheduler>();
            bool useBuffer = _configuration.GetValue<bool>("UseBufferMode", true);

            if (useBuffer)
            {
                scheduler.Schedule<AnnotationJob>()
                .EverySeconds(annotationPeriod)
                .PreventOverlapping(nameof(AnnotationJob));

                scheduler.Schedule<ArgumentationJob>()
                    .EverySeconds(annotationPeriod)
                    .PreventOverlapping(nameof(ArgumentationJob));

                scheduler.Schedule<GameNotarizationJob>()
                    .EverySeconds(annotationPeriod)
                    .PreventOverlapping(nameof(GameNotarizationJob));
            }
            else
            {
                scheduler.Schedule<DecisionSupportJob>()
                    .EverySeconds(annotationPeriod)
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
