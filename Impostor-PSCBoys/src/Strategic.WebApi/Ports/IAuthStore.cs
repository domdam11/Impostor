using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Strategic.WebApi.Models;

namespace Strategic.WebApi.Ports
{
    // =========================================================
    // Utenti & login
    // =========================================================
    /// <summary>
    /// Gestione utenti e autenticazione tramite <c>sessionKey</c>.
    /// </summary>
    public interface IAuthStore
    {
        Task<UserAuth?> GetUserAsync(string userId);
        Task<UserAuth> CreateUserAsync(string userId, string role = "user");   // genera & salva sessionKey
        Task<UserAuth?> ValidateLoginAsync(string userId, string sessionKey);
        Task<IReadOnlyList<UserAuth>> GetAllUsersAsync();                       // per GET /admin/users
        Task<string?> RotateSessionKeyAsync(string userId);                     // per rotate-session-key
    }
}
