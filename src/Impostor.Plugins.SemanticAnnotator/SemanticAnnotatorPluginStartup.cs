using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Coravel;

namespace Impostor.Plugins.SemanticAnnotator
{
    public class SemanticAnnotatorPluginStartup : IPluginStartup
    {
        public void ConfigureHost(IHostBuilder host)
        {

        }

        /// <summary>
        /// Configures the services required for the plugin.
        /// Registers dependencies using Dependency Injection.
        /// </summary>
        /// <param name="services">Service collection for dependency registration.</param>
        public void ConfigureServices(IServiceCollection services)
        {
            // Adds all necessary event listeners as singletons
            services.AddSingleton<IEventListener, GameEventListener>();
            services.AddSingleton<IEventListener, ClientEventListener>();
            services.AddSingleton<IEventListener, PlayerEventListener>();
            services.AddSingleton<IEventListener, MeetingEventListener>();
            services.AddSingleton<IEventListener, ShipEventListener>();

            // Adds the GameEventCacheManager as a singleton for managing event storage
            services.AddSingleton<GameEventCacheManager>();

            // Adds AnnotateTask for periodic annotation
            services.AddTransient<AnnotateTask>();

            // Adds AnnotatorEngine as a singleton for processing game event annotations
            services.AddSingleton<AnnotatorEngine>();

            // Configures Coravel for scheduling periodic tasks
            services.AddScheduler();
        }
    }
}
