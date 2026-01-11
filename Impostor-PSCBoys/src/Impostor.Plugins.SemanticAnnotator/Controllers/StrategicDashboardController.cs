using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using TransactionHandler.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Controllers
{
    [Route("ui")]
    public class StrategicDashboardController : ControllerBase
    {
        private readonly Assembly _assembly;

        public StrategicDashboardController()
        {
            
            var names = typeof(StrategicDashboardController).Assembly.GetManifestResourceNames();
            Console.WriteLine("=== Embedded resources ===");
            foreach (var n in names)
                Console.WriteLine(n);
            _assembly = typeof(StrategicDashboardController).Assembly;
        }

        [HttpGet("")]
        public IActionResult Index()
        {
            return ServeEmbedded("Impostor.Plugins.SemanticAnnotator.Resources.index.html", "text/html");
        }

        [HttpGet("f7-app.js")]
        public IActionResult AppJs()
        {
            return ServeEmbedded("Impostor.Plugins.SemanticAnnotator.Resources.f7-app.js", "application/javascript");
        }

        private IActionResult ServeEmbedded(string resourceName, string contentType)
        {
            var stream = _assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return NotFound($"Resource {resourceName} not found");
            return File(stream, contentType);
        }

        
    }
}
