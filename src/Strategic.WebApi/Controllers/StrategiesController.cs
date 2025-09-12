using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Strategic.WebApi.Models;
using Strategic.WebApi.Ports;
using Swashbuckle.AspNetCore.Annotations;

namespace Impostor.Plugins.SemanticAnnotator.Controllers
{
    [ApiController]
    [Route("api/strategic/strategies")]
    [Produces("application/json")]
    [Authorize]
    [ApiExplorerSettings(GroupName = "v1")]
    public sealed class StrategiesController : ControllerBase
    {
        private readonly IStrategyStore _store;

        public StrategiesController(IStrategyStore store)
        {
            _store = store;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Elenco strategie correnti")]
        [ProducesResponseType(typeof(IEnumerable<Strategy>), 200)]
        public async Task<IActionResult> GetAsync()
        {
            var list = await _store.GetAllAsync();
            return Ok(list);
        }
    }
}
