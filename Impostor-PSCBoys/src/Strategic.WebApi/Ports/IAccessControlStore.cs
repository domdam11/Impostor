using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Strategic.WebApi.Ports
{
    // =========================================================
    // Permessi (quali sessioni può votare un utente)
    // =========================================================
    /// <summary>
    /// Controllo accessi alle sessioni di voto.
    /// </summary>
    public interface IAccessControlStore
    {
        /// <summary>
        /// Elenco delle sessioni per cui l’utente è abilitato al voto.
        /// </summary>
        Task<IReadOnlyList<string>> GetAllowedSessionsAsync(string userId);

        /// <summary>
        /// Sostituisce completamente l’elenco delle sessioni abilitate per l’utente.
        /// Implementazioni dovrebbero garantire idempotenza.
        /// </summary>
        Task SetAllowedSessionsAsync(string userId, IEnumerable<string> sessionIds);

        /// <summary>
        /// Verifica se l’utente può votare nella sessione.
        /// Nota: gli <c>admin</c> devono essere sempre considerati abilitati (bypass).
        /// </summary>
        Task<bool> CanUserVoteAsync(string sessionId, string userId);
    }
}
