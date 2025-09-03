using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strategic.WebApi.Ports;

namespace Strategic.WebApi.Controllers
{
    [ApiController]
    [Route("api")]
    [Produces("application/json")]
    [Authorize]
    public class PermissionsController : ControllerBase
    {
        private readonly IAccessControlStore _access;
        private readonly IAuthStore _auth;
        private readonly IGameEventStorage _sessions; // per validare i sessionId

        public PermissionsController(
            IAccessControlStore access,
            IAuthStore auth,
            IGameEventStorage sessions)
        {
            _access = access;
            _auth = auth;
            _sessions = sessions;
        }

        public sealed class AllowedSessionsBody
        {
            [JsonPropertyName("sessionIds")]
            public List<string>? SessionIds { get; set; }
        }

        /// <summary>
        /// GET /api/admin/users/{userId}/allowed-sessions -> { userId, sessionIds:[...] }
        /// </summary>
        [Authorize(Roles = "admin")]
        [HttpGet("admin/users/{userId}/allowed-sessions")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> GetAllowedSessions(string userId)
        {
            var set = await _access.GetAllowedSessionsAsync(userId) ?? Array.Empty<string>();
            return Ok(new { userId, sessionIds = set });
        }

        /// <summary>
        /// PUT /api/admin/users/{userId}/allowed-sessions   body:{ "sessionIds": ["..."] }
        /// Filtra gli id non esistenti e rimuove duplicati.
        /// </summary>
        [Authorize(Roles = "admin")]
        [HttpPut("admin/users/{userId}/allowed-sessions")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> PutAllowedSessions(string userId, [FromBody] AllowedSessionsBody body)
        {
            if (body?.SessionIds is null)
                return BadRequest("sessionIds required");

            // normalizza input: trim + dedup
            var desired = new HashSet<string>(
                body.SessionIds
                    .Where(s => !string.IsNullOrWhiteSpace(s))
                    .Select(s => s.Trim()),
                StringComparer.Ordinal
            );

            // valida i sessionId esistenti
            var all = await _sessions.GetAllSessionsAsync() ?? new List<GameSessionInfo>();
            var valid = new HashSet<string>(all.Select(s => s.Id), StringComparer.Ordinal);
            var filtered = desired.Where(valid.Contains).ToArray();

            await _access.SetAllowedSessionsAsync(userId, filtered);
            return Ok(new { userId, sessionIds = filtered });
        }

        /// <summary>
        /// GET /api/strategic/session/{sessionId}/access/{userId} -> { userId, sessionId, canVote }
        /// Admin: canVote = true (bypass; associato implicitamente a tutte le session).
        /// </summary>
        [Authorize]
        [HttpGet("strategic/session/{sessionId}/access/{userId}")]
        [ProducesResponseType(typeof(object), 200)]
        public async Task<IActionResult> CheckAccess(string sessionId, string userId)
        {
            var u = await _auth.GetUserAsync(userId);
            if (u is null)
                return Ok(new { userId, sessionId, canVote = false });

            if (string.Equals(u.Role, "admin", StringComparison.OrdinalIgnoreCase))
                return Ok(new { userId, sessionId, canVote = true });

            var allowed = await _access.CanUserVoteAsync(sessionId, userId);
            return Ok(new { userId, sessionId, canVote = allowed });
        }
    }
}
