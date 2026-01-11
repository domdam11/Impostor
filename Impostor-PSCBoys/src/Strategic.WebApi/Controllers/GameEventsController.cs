using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Strategic.WebApi.Controllers
{
    [ApiController]
    [Route("api/strategic")]
    [Authorize]
    public class GameEventsController : ControllerBase
    {
        private readonly IGameEventStorage _storage;

        public GameEventsController(IGameEventStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Restituisce la lista degli eventi annotati con score e strategia.
        /// </summary>
        [HttpGet("session/{sessionId}/eventlist")]
        public async Task<IActionResult> GetEventListAsync(string sessionId)
        {
            var userId = GetUserIdFromClaims(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();
            var list = await _storage.GetEventListAsync(sessionId);
            if (list == null || !list.Any())
                return NotFound(new { message = "No events found for session." });

            return Ok(list);
        }

        private static string? GetUserIdFromClaims(ClaimsPrincipal user)
            => user.FindFirstValue(ClaimTypes.NameIdentifier)
               ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        /// <summary>
        /// Restituisce i dettagli dell’evento per una sessione (grafo e strategie).
        /// </summary>
        [HttpGet("session/{sessionId}/eventdetails/{eventId}")]
        public async Task<IActionResult> GetEventDetailsAsync(string sessionId, string eventId)
        {
            var userId = GetUserIdFromClaims(User);
            if (string.IsNullOrWhiteSpace(userId))
                return Unauthorized();
            var details = await _storage.GetEventDetailsAsync(sessionId, eventId);
            if (details == null)
                return NotFound(new { message = "Event not found." });

            return Ok(details);
        }

        /// <summary>
        /// (Facoltativo) Restituisce tutte le sessioni registrate.
        /// </summary>
        [HttpGet("sessions")]
        public async Task<IActionResult> GetAllSessionsAsync()
        {
            var sessions = await _storage.GetAllSessionsAsync();
            if (sessions == null || !sessions.Any())
                return NotFound(new { message = "No sessions found." });

            return Ok(sessions);
        }
    }
}
