using Impostor.Api.Events;
using Impostor.Api.Plugins;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator;
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

        // Metodo utilizzato per configurare i servizi del plugin e per registrare le dipendenze utilizzando la Dependency Injection
        public void ConfigureServices(IServiceCollection services)
        {
            // Aggiunge tutti i listener di eventi necessari
            services.AddSingleton<IEventListener, GameEventListener>();
            services.AddSingleton<IEventListener, ClientEventListener>();
            services.AddSingleton<IEventListener, PlayerEventListener>();
            services.AddSingleton<IEventListener, MeetingEventListener>();
            services.AddSingleton<IEventListener, ShipEventListener>();

            // Aggiunge il GameEventCacheManager come singleton per la gestione degli eventi
            services.AddSingleton<GameEventCacheManager>();

            // Aggiunge AnnotateTask per il salvataggio periodico
            services.AddTransient<AnnotateTask>();

            // Configura Coravel per la pianificazione dei task periodici
            services.AddScheduler();
        }
    }
}
