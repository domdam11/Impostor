using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Strategic.WebApi.Models;

namespace Strategic.WebApi.Ports
{
    // =========================================================
    // Valutazioni (like/dislike)
    // =========================================================
    /// <summary>
    /// CRUD append-like e letture su valutazioni degli eventi.
    /// </summary>
    public interface IEvaluationStore
    {
        /// <summary>
        /// Restituisce la valutazione di <paramref name="userId"/> su <paramref name="eventId"/> nella <paramref name="sessionId"/>, se presente.
        /// </summary>
        Task<Evaluation?> GetEvaluationAsync(string sessionId, string eventId, string userId);

        /// <summary>
        /// Restituisce tutte le valutazioni dell’utente in una sessione.
        /// </summary>
        Task<IReadOnlyList<Evaluation>> GetEvaluationsByUserAsync(string sessionId, string userId);

        /// <summary>
        /// Restituisce tutte le valutazioni degli utenti per un evento.
        /// </summary>
        Task<IReadOnlyList<Evaluation>> GetEvaluationsByEventAsync(string sessionId, string eventId);

        /// <summary>
        /// Crea una valutazione. Ritorna <c>false</c> se la valutazione esiste già.
        /// </summary>
        Task<bool> CreateEvaluationAsync(Evaluation e);

        /// <summary>
        /// Aggiorna una valutazione esistente. Ritorna <c>false</c> se non esiste.
        /// </summary>
        Task<bool> UpdateEvaluationAsync(Evaluation e);

        /// <summary>
        /// Elimina una valutazione (se presente).
        /// </summary>
        Task<bool> DeleteEvaluationAsync(string sessionId, string eventId, string userId);

        /// <summary>
        /// Conta i like/dislike aggregati di un evento (tutti gli utenti).
        /// </summary>
        Task<(int like, int dislike)> CountByEventAsync(string sessionId, string eventId);

        /// <summary>
        /// (Opzionale) Conta i like/dislike per più eventi in una sola chiamata.
        /// Implementazione di default usa <see cref="CountByEventAsync"/> in loop.
        /// Gli store possono fare override con una versione ottimizzata.
        /// </summary>
        public virtual async Task<IDictionary<string, (int like, int dislike)>> CountByEventsAsync(
            string sessionId, IEnumerable<string> eventIds)
        {
            var result = new Dictionary<string, (int like, int dislike)>();
            if (eventIds is null) return result;

            var unique = eventIds.Distinct().ToArray();
            foreach (var eid in unique)
            {
                var (like, dislike) = await CountByEventAsync(sessionId, eid);
                result[eid] = (like, dislike);
            }
            return result;
        }
    }
}
