using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;
using Strategic.WebApi.Security;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Strategic.WebApi.Ports;
using Strategic.WebApi.Models; // <-- estensioni GetUserId(), IsAdmin(), IsAuthenticated()

namespace Impostor.Plugins.SemanticAnnotator.Controllers
{
    [ApiController]
    [Route("api/strategic")]
    [Produces("application/json")]
    [Authorize] // tutti gli endpoint richiedono autenticazione
    [ApiExplorerSettings(GroupName = "v1")]
    public class EvaluationsController : ControllerBase
    {
        private readonly IEvaluationStore _evals;
        private readonly IGameEventStorage _events;

        public EvaluationsController(IEvaluationStore evals, IGameEventStorage events)
        {
            _evals = evals;
            _events = events;
        }

        public sealed class EvaluationBody
        {
            [JsonPropertyName("reaction")] public string? Reaction { get; set; } // "like" | "dislike" | null
            [JsonPropertyName("value")] public int? Value { get; set; }          // 1 | -1 | 0
            [JsonPropertyName("rating")] public int? Rating { get; set; }        // opzionale
            [JsonPropertyName("timestamp")] public string? Timestamp { get; set; }
            [JsonPropertyName("userId")] public string? UserId { get; set; }     // usata nel POST per compatibilità mock (vedi sotto)
        }

        private static int NormalizeValue(string? reaction, int? value)
            => value ?? (reaction == "like" ? 1 : reaction == "dislike" ? -1 : 0);

        private static string? NormalizeReaction(string? reaction, int value)
            => !string.IsNullOrWhiteSpace(reaction) ? reaction : value > 0 ? "like" : value < 0 ? "dislike" : null;

        // ----------------------------------------------------
        // LISTE
        // ----------------------------------------------------

        /// <summary>
        /// Lista delle valutazioni dell’utente nella sessione.
        /// </summary>
        /// <remarks>
        /// - Utente normale: restituisce <b>le proprie</b> valutazioni (userId preso dal token).<br/>
        /// - <b>Admin</b>: può passare <c>?userId=</c> per consultare un altro utente.
        /// </remarks>
        [HttpGet("session/{sessionId}/evaluations")]
        [SwaggerOperation(Summary = "Lista valutazioni utente per sessione (admin: può filtrare per userId)")]
        [ProducesResponseType(typeof(IEnumerable<Evaluation>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetEvalsAsync(
            string sessionId,
            [FromQuery, SwaggerParameter("Solo admin: id dell’utente da ispezionare", Required = false)]
            string? userId = null)
        {
            string effectiveUserId;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                if (!User.IsAdmin())
                    return Forbid();

                effectiveUserId = userId.Trim();
            }
            else
            {
                effectiveUserId = User.GetUserId() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(effectiveUserId))
                    return Unauthorized();
            }

            var list = await _evals.GetEvaluationsByUserAsync(sessionId, effectiveUserId);
            return Ok(list ?? new List<Evaluation>());
        }

        /// <summary>
        /// Tutte le valutazioni di tutti gli utenti per un evento.
        /// </summary>
        [HttpGet("session/{sessionId}/event/{eventId}/evaluations")]
        [SwaggerOperation(Summary = "Tutte le valutazioni per un evento (conteggio per admin/monitoring)")]
        [ProducesResponseType(typeof(IEnumerable<Evaluation>), 200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetEventEvalsAsync(string sessionId, string eventId)
        {
            var list = await _evals.GetEvaluationsByEventAsync(sessionId, eventId);
            return Ok(list ?? new List<Evaluation>());
        }

        // ----------------------------------------------------
        // CRUD
        // ----------------------------------------------------

        /// <summary>Crea una valutazione (409 se già esiste).</summary>
        /// <remarks>
        /// Per compatibilità col mock, se l’utente è <b>admin</b> può specificare <c>userId</c> nel body per creare a nome di altri;
        /// altrimenti l’azione viene sempre eseguita a nome dell’utente loggato (ignorando l’eventuale userId nel body).
        /// </remarks>
        [HttpPost("session/{sessionId}/event/{eventId}/evaluations")]
        [SwaggerOperation(Summary = "Crea una valutazione (UserId nel body solo per admin; altrimenti dal token)")]
        [ProducesResponseType(typeof(Evaluation), 201)]
        [ProducesResponseType(401)]
        [ProducesResponseType(409)]
        public async Task<IActionResult> CreateEvalAsync(string sessionId, string eventId, [FromBody] EvaluationBody body)
        {
            var requesterId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(requesterId)) return Unauthorized();

            var targetUserId = User.IsAdmin() && !string.IsNullOrWhiteSpace(body?.UserId)
                ? body!.UserId!.Trim()
                : requesterId;

            var exists = await _evals.GetEvaluationAsync(sessionId, eventId, targetUserId);
            if (exists is not null) return Conflict("Already exists");

            var value = NormalizeValue(body?.Reaction, body?.Value);
            var reaction = NormalizeReaction(body?.Reaction, value);

            var ev = new Evaluation
            {
                Id = $"{targetUserId}-{eventId}",
                SessionId = sessionId,
                EventId = eventId,
                UserId = targetUserId,
                Reaction = reaction,
                Value = value,
                Timestamp = !string.IsNullOrWhiteSpace(body?.Timestamp) ? body!.Timestamp! : DateTime.UtcNow.ToString("o")
            };

            var ok = await _evals.CreateEvaluationAsync(ev);
            if (!ok) return StatusCode(500, "Create failed");

            return Created($"/api/strategic/session/{sessionId}/event/{eventId}/evaluation/{targetUserId}", ev);
        }

        /// <summary>Aggiorna una valutazione esistente (solo proprietario; userId dal token).</summary>
        [HttpPut("session/{sessionId}/event/{eventId}/evaluation")]
        [SwaggerOperation(Summary = "Aggiorna una valutazione (userId dal token; non accetta userId in input)")]
        [ProducesResponseType(typeof(Evaluation), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(404)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> UpdateEvalAsync(string sessionId, string eventId, [FromBody] EvaluationBody body)
        {
            var userId = User.GetUserId();
            if (string.IsNullOrWhiteSpace(userId)) return Unauthorized();

            // L’utente può aggiornare solo la propria valutazione
            var exists = await _evals.GetEvaluationAsync(sessionId, eventId, userId);
            if (exists is null) return NotFound("Not found");

            var value = NormalizeValue(body?.Reaction, body?.Value);
            var reaction = NormalizeReaction(body?.Reaction, value);

            var ev = new Evaluation
            {
                Id = $"{userId}-{eventId}",
                SessionId = sessionId,
                EventId = eventId,
                UserId = userId,
                Reaction = reaction,
                Value = value,
                Timestamp = !string.IsNullOrWhiteSpace(body?.Timestamp) ? body!.Timestamp! : DateTime.UtcNow.ToString("o")
            };

            var ok = await _evals.UpdateEvaluationAsync(ev);
            if (!ok) return StatusCode(500, "Update failed");
            return Ok(ev);
        }

        /// <summary>Elimina la valutazione. Admin può passare <c>?userId=</c>, altrimenti vale l’utente del token.</summary>
        [HttpDelete("session/{sessionId}/event/{eventId}/evaluation")]
        [SwaggerOperation(Summary = "Elimina la valutazione (admin: può specificare userId query; altri: dal token)")]
        [ProducesResponseType(204)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteEvalAsync(
            string sessionId,
            string eventId,
            [FromQuery, SwaggerParameter("Solo admin: id dell’utente da cui cancellare", Required = false)]
            string? userId = null)
        {
            string targetUserId;

            if (!string.IsNullOrWhiteSpace(userId))
            {
                if (!User.IsAdmin())
                    return Forbid();

                targetUserId = userId.Trim();
            }
            else
            {
                targetUserId = User.GetUserId() ?? string.Empty;
                if (string.IsNullOrWhiteSpace(targetUserId))
                    return Unauthorized();
            }

            var ok = await _evals.DeleteEvaluationAsync(sessionId, eventId, targetUserId);
            if (!ok) return NotFound("Not found");
            return NoContent();
        }

        // ----------------------------------------------------
        // Totali (solo admin)
        // ----------------------------------------------------

        /// <summary>Totali like/dislike per un evento (solo admin).</summary>
        [Authorize(Roles = "admin")]
        [HttpGet("session/{sessionId}/event/{eventId}/totals")]
        [SwaggerOperation(Summary = "Totali per evento (admin)")]
        [ProducesResponseType(typeof(object), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetEventTotalsAsync(string sessionId, string eventId)
        {
            var (like, dislike) = await _evals.CountByEventAsync(sessionId, eventId);
            return Ok(new { like, dislike });
        }

        /// <summary>Totali per tutti gli eventi della sessione (solo admin).</summary>
        [Authorize(Roles = "admin")]
        [HttpGet("session/{sessionId}/evaluations/totals")]
        [SwaggerOperation(Summary = "Totali per sessione (admin)")]
        [ProducesResponseType(typeof(Dictionary<string, object>), 200)]
        [ProducesResponseType(401)]
        [ProducesResponseType(403)]
        public async Task<IActionResult> GetSessionTotalsAsync(string sessionId)
        {
            var list = await _events.GetEventListAsync(sessionId);
            var dict = new Dictionary<string, object>();

            foreach (var e in list)
            {
                var (like, dislike) = await _evals.CountByEventAsync(sessionId, e.Id);
                dict[e.Id] = new { like, dislike };
            }

            return Ok(dict);
        }
    }
}
