using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    /// <summary>
    /// Porta per la memorizzazione persistente e append-only degli eventi di gioco, ad esempio in Redis.
    /// </summary>
    public interface IGameEventStorage
    {
        // ---- Scrittura append-only ----

        /// <summary>
        /// Registra l’inizio di una nuova sessione di gioco.
        /// </summary>
        Task CreateGameSessionAsync(string sessionId, string description);

        /// <summary>
        /// Registra un nuovo giocatore per una sessione.
        /// </summary>
        Task AddPlayerAsync(string sessionId, string playerName, string metadata);

        /// <summary>
        /// Registra un evento annotato all’interno della sessione di gioco.
        /// </summary>
        Task CreateEventAsync(string sessionId, string eventId, string annotatedReasoning, string metadata);

        // ---- Lettura ----

        /// <summary>
        /// Recupera l’elenco degli eventi (con punteggio e strategia) per una sessione.
        /// </summary>
        Task<List<StrategicEventSummary>> GetEventListAsync(string sessionId);

        /// <summary>
        /// Recupera i dettagli (grafo + strategie) di un singolo evento della sessione.
        /// </summary>
        Task<StrategicEventDetails> GetEventDetailsAsync(string sessionId, string eventId);

        /// <summary>
        /// (Facoltativo) Restituisce tutte le sessioni registrate.
        /// </summary>
        Task<List<GameSessionInfo>> GetAllSessionsAsync();
    }

}
