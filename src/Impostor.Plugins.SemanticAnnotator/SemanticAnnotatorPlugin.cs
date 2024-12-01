using Impostor.Api.Plugins;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.DependencyInjection;
using Coravel;
using Coravel.Scheduling.Schedule.Interfaces;
using System.Threading.Tasks;
using System;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.SemanticAnnotator
{
    [ImpostorPlugin("gg.impostor.example")]
    public class SemanticAnnotatorPlugin : PluginBase
    {
        private readonly ILogger<SemanticAnnotatorPlugin> _logger;
        private readonly GameEventCacheManager _eventCacheManager;
        private readonly IServiceProvider _serviceProvider;

        // Costruttore con iniezione delle dipendenze
        public SemanticAnnotatorPlugin(ILogger<SemanticAnnotatorPlugin> logger, GameEventCacheManager eventCacheManager, IServiceProvider serviceProvider)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
            _serviceProvider = serviceProvider;
        }

        // Metodo asincrono che si attiva quando il plugin viene abilitato
        public override ValueTask EnableAsync()
        {
            _logger.LogInformation("SemanticAnnotator is being enabled.");

            // Recupera il periodo di annotazione dai parametri di ambiente o usa un valore predefinito
            int annotationPeriodInSeconds = int.Parse(Environment.GetEnvironmentVariable("ANNOTATION_PERIOD") ?? "30");

            // Risoluzione del servizio IScheduler
            var scheduler = _serviceProvider.GetRequiredService<IScheduler>();

            // Programma l'esecuzione periodica del task di annotazione
            scheduler.Schedule(() => AnnotateSessions()).EverySeconds(annotationPeriodInSeconds);

            return default;
        }

        // Metodo asincrono che si attiva quando il plugin viene disabilitato
        public override ValueTask DisableAsync()
        {
            _logger.LogInformation("SemanticAnnotator is being disabled.");
            return default;
        }

        // Metodo che esegue l'annotazione per tutte le sessioni attive
        private async Task AnnotateSessions()
        {
            // Recupera tutte le sessioni attive
            var activeSessions = _eventCacheManager.GetActiveSessions();

            foreach (var gameCode in activeSessions)
            {
                // Recupera gli eventi per la sessione specifica
                var events = await _eventCacheManager.GetEventsByGameCodeAsync(gameCode);

                // Crea e avvia l'annotatore per ogni sessione
                var annotator = _serviceProvider.GetRequiredService<AnnotateTask>();
                await annotator.AnnotateAsync(gameCode);  // Passa gli eventi per l'annotazione
            }
        }
    }
}
