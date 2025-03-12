using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Coravel;
using Coravel.Scheduling.Schedule.Interfaces;
using System.Threading.Tasks;
using System;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.SemanticAnnotator
{
    [ImpostorPlugin("gg.impostor.example")]
    public class SemanticAnnotatorPlugin : PluginBase
    {
        private readonly ILogger<SemanticAnnotatorPlugin> _logger;
        private readonly GameEventCacheManager _eventCacheManager;
        private readonly IServiceProvider _serviceProvider;

        /// <summary>
        /// Constructor with dependency injection.
        /// </summary>
        /// <param name="logger">Handles logging for debugging and monitoring.</param>
        /// <param name="eventCacheManager">Manages cached game events.</param>
        /// <param name="serviceProvider">Provides services and dependency resolution.</param>
        public SemanticAnnotatorPlugin(ILogger<SemanticAnnotatorPlugin> logger, GameEventCacheManager eventCacheManager, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
            _serviceProvider = serviceProvider;
        }

        /// <summary>
        /// Asynchronous method triggered when the plugin is enabled.
        /// </summary>
        /// <returns>Asynchronous ValueTask.</returns>
        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("SemanticAnnotator is being enabled.");

            // Retrieve the annotation period from environment variables or use a default value
            int annotationPeriodInSeconds = int.Parse(Environment.GetEnvironmentVariable("ANNOTATION_PERIOD") ?? "30");

            // Resolve the IScheduler service
            var scheduler = _serviceProvider.GetRequiredService<IScheduler>();

            // Schedule the periodic execution of the annotation task
            scheduler.Schedule(() => AnnotateSessions()).EverySeconds(annotationPeriodInSeconds);

            return default;
        }

        /// <summary>
        /// Asynchronous method triggered when the plugin is disabled.
        /// </summary>
        /// <returns>Asynchronous ValueTask.</returns>
        public override ValueTask DisableAsync()
        {
            _logger.LogInformation("SemanticAnnotator is being disabled.");
            return default;
        }

        /// <summary>
        /// Executes the annotation for all active sessions.
        /// </summary>
        /// <returns>Asynchronous Task.</returns>
        public async Task AnnotateSessions()
        {
            // Retrieve all active game sessions
            var activeSessions = _eventCacheManager.GetActiveSessions();

            foreach (var gameCode in activeSessions)
            {
                // Retrieve events for the specific session
                //var events = await _eventCacheManager.GetEventsByGameCodeAsync(gameCode);

                // Resolve and execute the annotation task for each session
                var annotator = _serviceProvider.GetRequiredService<AnnotateTask>();
                await annotator.AnnotateAsync(gameCode);  // Passa gli eventi per l'annotazione
            }

        }
    }
}
