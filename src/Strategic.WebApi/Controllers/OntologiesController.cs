using CowlSharp.Wrapper;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strategic.WebApi.Models;
using Strategic.WebApi.Ports;
using Swashbuckle.AspNetCore.Annotations;

namespace Impostor.Plugins.SemanticAnnotator.Controllers
{
    [ApiController]
    [Route("api/strategic/ontologies")]
    [Produces("application/json")]
    [Authorize]
    [ApiExplorerSettings(GroupName = "v1")]
    public sealed class OntologiesController : ControllerBase
    {
        private readonly IOntologyStore _store;

        public OntologiesController(IOntologyStore store)
        {
            _store = store;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Elenco versioni ontologia salvate")]
        [ProducesResponseType(typeof(IEnumerable<OntologyVersion>), 200)]
        public async Task<IActionResult> ListAsync()
        {
            var list = await _store.GetAllAsync();
            return Ok(list);
        }

        [HttpGet("{id}")]
        [SwaggerOperation(Summary = "Recupera una versione specifica")]
        [ProducesResponseType(typeof(OntologyVersion), 200)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> GetAsync(string id)
        {
            var v = await _store.GetAsync(id);
            return v is null ? NotFound() : Ok(v);
        }

        [Authorize(Roles = "Admin")]
        [HttpPost("upload")]
        [SwaggerOperation(Summary = "Carica una nuova ontologia OWL da file e aggiorna le strategie")]
        [ProducesResponseType(typeof(object), 201)]
        public async Task<IActionResult> UploadAsync(IFormFile file,
                                                     [FromServices] IStrategyStore strategyStore)
        {
            if (file == null || file.Length == 0)
                return BadRequest("File mancante o vuoto.");

            string owlContent;
            using (var reader = new StreamReader(file.OpenReadStream()))
            {
                owlContent = await reader.ReadToEndAsync();
            }

            // 1. Salva nuova versione
            var v = await _store.SaveAsync(owlContent);

            // 2. Estrai strategie dal payload OWL
            var strategies = ExtractStrategiesFromOwl(owlContent);

            // 3. Aggiorna Redis
            await strategyStore.SaveAllAsync(strategies);

            return Created($"/api/strategic/ontologies/{v.Id}", v);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Admin")]
        [SwaggerOperation(Summary = "Cancella una versione specifica")]
        [ProducesResponseType(204)]
        [ProducesResponseType(404)]
        public async Task<IActionResult> DeleteAsync(string id)
        {
            var ok = await _store.DeleteAsync(id);
            return ok ? NoContent() : NotFound();
        }

        // --------------------------------------------------------
        // 👇 placeholder parser strategie da OWL
        private static List<Strategy> ExtractStrategiesFromOwl(string owl)
        {
            var list = new CowlWrapper().ReadIndividuals(owl);
            var strategies = new List<Strategy>();
            // In produzione: usa libreria OWL / RDF
            // Per ora: mock semplice
            foreach (var item in list)
            {
                strategies.Add(new Strategy
                {
                    Id = item.Replace(" ", "").Replace("-", "").Replace("_", ""),
                    Name = item,
                    Description = $"Strategia generata automaticamente per '{item}'."
                });
                Console.WriteLine($"[DEBUG] Individual: {item}");
            }
           return strategies;
        }
    }
}
