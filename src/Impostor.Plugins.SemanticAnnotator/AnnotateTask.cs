using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Coravel.Invocable;
using Impostor.Plugins.SemanticAnnotator.Utils;

namespace Impostor.Plugins.SemanticAnnotator
{
    // Implementa l'interfaccia IInvocable per la gestione delle annotazioni periodiche
    public class AnnotateTask : IInvocable
    {
        private readonly GameEventCacheManager _eventCacheManager;
        private readonly CowlWrapper _cowlWrapper;
        private readonly ILogger<AnnotateTask> _logger;

        // Costruttore con iniezione delle dipendenze
        public AnnotateTask(GameEventCacheManager eventCacheManager, CowlWrapper cowlWrapper, ILogger<AnnotateTask> logger)
        {
            _eventCacheManager = eventCacheManager;
            _cowlWrapper = cowlWrapper;
            _logger = logger;
        }

        // Metodo per l'annotazione asincrona degli eventi di una sessione
        public async Task AnnotateAsync(string gameCode)
        {
            // Recupera gli eventi dalla cache usando il GameEventCacheManager
            var cachedEvents = await _eventCacheManager.GetEventsByGameCodeAsync(gameCode);

            // Controlla la presenza di eventi
            if (cachedEvents == null || cachedEvents.Count == 0)
            {
                _logger.LogInformation($"Nessun evento trovato nella cache per il GameCode: {gameCode}.");
                return;
            }

            // Passa gli eventi al CowlWrapper per l'annotazione
            await _cowlWrapper.Annotate(gameCode);

            // Cancella gli eventi dalla cache una volta completata l'annotazione
            await _eventCacheManager.ClearGameEventsAsync(gameCode);

            // Log di completamento annotazione
            _logger.LogInformation($"Annotazione completata per GameCode: {gameCode}.");
        }

        // Metodo invocabile da Coravel per annotare tutte le sessioni attive
        public async Task Invoke()
        {
            var activeSessions = await _eventCacheManager.GetAllEventsAsync();

            if (activeSessions.Count == 0)
            {
                _logger.LogInformation("Nessuna sessione attiva trovata per l'annotazione.");
                return;
            }

            _logger.LogInformation($"Annotazione avviata per {activeSessions.Count} sessioni attive.");

            foreach (var session in activeSessions)
            {
                var gameCode = session.Key;
                await AnnotateAsync(gameCode);
            }

            _logger.LogInformation("Annotazione completata per tutte le sessioni attive.");
        }
    }
}
