using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strategic.WebApi.Models;
using Strategic.WebApi.Ports;
using Strategic.WebApi.Security;

namespace Strategic.WebApi.Controllers
{
    [ApiController]
    [Route("api/strategic")]
    [Produces("application/json")]
    [Authorize]
    [ApiExplorerSettings(GroupName = "v1")]
    public class CorrectionsController : ControllerBase
    {
        private readonly ICorrectionStore _corrections;

        public CorrectionsController(ICorrectionStore corrections)
        {
            _corrections = corrections;
        }

        public sealed class CorrectionBody
        {
            [JsonPropertyName("correctStrategy")] public string? CorrectStrategy { get; set; }
            [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
        }

        /// <summary>Ottieni la correzione dell’utente per un evento.</summary>
        [HttpGet("session/{sessionId}/event/{eventId}/correction")]
        [ProducesResponseType(typeof(Correction), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetCorrectionAsync(string sessionId, string eventId)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var corr = await _corrections.GetCorrectionAsync(sessionId, eventId, userId);
            if (corr is null) return NotFound();
            return Ok(corr);
        }

        /// <summary>Crea o aggiorna la correzione (sempre a nome del token).</summary>
        [HttpPost("session/{sessionId}/event/{eventId}/correction")]
        [ProducesResponseType(typeof(Correction), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(400)]
        public async Task<IActionResult> SaveCorrectionAsync(string sessionId, string eventId, [FromBody] CorrectionBody body)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();
            if (string.IsNullOrWhiteSpace(body?.CorrectStrategy)) return BadRequest("Missing strategy");

            var corr = new Correction
            {
                Id = $"{userId}-{eventId}",
                SessionId = sessionId,
                EventId = eventId,
                UserId = userId,
                CorrectStrategy = body!.CorrectStrategy!,
                Timestamp = !string.IsNullOrWhiteSpace(body.Timestamp) ? body.Timestamp! : DateTime.UtcNow.ToString("o")
            };

            var ok = await _corrections.UpsertCorrectionAsync(corr);
            if (!ok) return StatusCode(500, "Save failed");

            return Ok(corr);
        }

        /// <summary>Elimina la correzione.</summary>
        [HttpDelete("session/{sessionId}/event/{eventId}/correction")]
        [ProducesResponseType(204)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteCorrectionAsync(string sessionId, string eventId)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            var ok = await _corrections.DeleteCorrectionAsync(sessionId, eventId, userId);
            if (!ok) return NotFound();
            return NoContent();
        }
    }

}
