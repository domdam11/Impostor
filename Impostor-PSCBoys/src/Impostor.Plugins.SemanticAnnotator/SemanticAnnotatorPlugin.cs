using System;
using System.Threading.Tasks;
using Coravel.Scheduling.Schedule;
using Coravel.Scheduling.Schedule.Interfaces;
using Impostor.Api.Plugins;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Application;
using Impostor.Plugins.SemanticAnnotator.Jobs;
using Impostor.Plugins.SemanticAnnotator.Models.Options;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Plugins.SemanticAnnotator
{
    [ImpostorPlugin("impostor.plugins.semanticannotator")]
    public class SemanticAnnotatorPlugin : PluginBase
    {
        private readonly DecisionSupportScheduler _scheduler;
        private readonly ILogger<SemanticAnnotatorPlugin> _logger;
        private readonly bool _useBuffer;
        //private readonly int _annotationIntervalMs;

        public SemanticAnnotatorPlugin(ILogger<SemanticAnnotatorPlugin> logger, IServiceProvider serviceProvider, IConfiguration _configuration, IOptions<SemanticPluginOptions> options, DecisionSupportScheduler scheduler)
        {
            _logger = logger;
            //_serviceProvider = serviceProvider;
            _useBuffer = options.Value.UseBufferMode;
            //_annotationIntervalMs = options.Value.AnnotationIntervalMs;
            _scheduler = scheduler;
        }

        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("[SemanticAnnotatorPlugin] EnableAsync called.");

            //var scheduler = _serviceProvider.GetRequiredService<IScheduler>();

            if (_useBuffer)
            {
               /* scheduler.Schedule<AnnotationJob>()
                .EverySeconds(_annotationIntervalMs)
                .PreventOverlapping(nameof(AnnotationJob));

                scheduler.Schedule<ArgumentationJob>()
                    .EverySeconds(_annotationIntervalMs)
                    .PreventOverlapping(nameof(ArgumentationJob));

                scheduler.Schedule<GameNotarizationJob>()
                    .EverySeconds(_annotationIntervalMs)
                    .PreventOverlapping(nameof(GameNotarizationJob));*/
            }
            else
            {
                _logger.LogInformation("No buffer mode.");
                _scheduler.Start();
                /*scheduler.Schedule<DecisionSupportJob>()
                    .EverySeconds(_annotationIntervalMs / 1000)
                    .PreventOverlapping(nameof(DecisionSupportJob));*/

            }


            return default;
        }

        public override ValueTask DisableAsync()
        {
            _logger.LogInformation("[SemanticAnnotatorPlugin] DisableAsync called.");
            _scheduler.Stop();
            return default;
        }
    }
}
