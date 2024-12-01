using Impostor.Api.Events;
using Impostor.Api.Events.Client;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class ClientEventListener : IEventListener
    {
        //Campo privato per registrare i log relativi all'attività del listener
        private readonly ILogger<ClientEventListener> _logger;
        //Campo privato per gestire la cache degli eventi raccolti
        private readonly GameEventCacheManager _eventCacheManager;

        // Iniezione della dipendenza per il logging e la cache degli eventi
        public ClientEventListener(ILogger<ClientEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        [EventListener]
        public async Task OnClientConnected(IClientConnectedEvent e)
        {
            // Determina il GameCode in base al contesto disponibile
            string gameCode = e.Client?.Game?.Code ?? "unassigned";

            // Crea un dizionario per rappresentare le informazioni sull'evento
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Usa un GUID come EventId
                { "GameCode", gameCode },                  // Salva il gameCode
                { "EventType", "ClientConnected" },        // Tipo di evento
                { "Timestamp", DateTime.UtcNow },          // Timestamp dell'evento
                { "ClientName", e.Client.Name },
                { "ClientLanguage", e.Client.Language },
                { "ChatMode", e.Client.ChatMode}
            };

            // Salva l'evento nella cache
            await _eventCacheManager.AddEventAsync(gameCode, eventData);

            // Log dell'evento
            _logger.LogInformation(
                "Client {name} > connected (language: {language}, chat mode: {chatMode})",
                e.Client.Name, e.Client.Language, e.Client.ChatMode);
        }

    }
}
