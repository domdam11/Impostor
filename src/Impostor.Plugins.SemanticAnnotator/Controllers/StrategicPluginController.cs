using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Controllers
{
    [ApiController]
    [Route("api/strategic")]
    public class StrategicPluginController : ControllerBase
    {
        private readonly IGameEventStorage _storage;

        public StrategicPluginController(IGameEventStorage storage)
        {
            _storage = storage;
        }

        /// <summary>
        /// Restituisce la lista degli eventi annotati con score e strategia.
        /// </summary>
        [HttpGet("session/{sessionId}/eventlist")]
        public async Task<IActionResult> GetEventListAsync(string sessionId)
        {
            var list = await _storage.GetEventListAsync(sessionId);
            if (list == null || !list.Any())
                return NotFound(new { message = "No events found for session." });

            return Ok(list);
        }

        /// <summary>
        /// Restituisce i dettagli dell’evento per una sessione (grafo e strategie).
        /// </summary>
        [HttpGet("session/{sessionId}/eventdetails/{eventId}")]
        public async Task<IActionResult> GetEventDetailsAsync(string sessionId, string eventId)
        {
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
