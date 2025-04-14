using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Coravel.Invocable;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class AnnotationJob : IInvocable
    {
        private readonly IAnnotationBuffer _buffer;
        private readonly IAnnotator _annotator;
        private readonly ILogger<AnnotationJob> _logger;
        private readonly GameEventCacheManager _cache;

        public AnnotationJob(
            GameEventCacheManager cache,
            IAnnotationBuffer buffer,
            IAnnotator annotator,
            ILogger<AnnotationJob> logger)
        {
            _cache = cache;
            _buffer = buffer;
            _annotator = annotator;
            _logger = logger;
        }

        public async Task Invoke()
        {
            foreach (var gameCode in _cache.GetActiveSessions())
            {
                await _annotator.AnnotateAsync(gameCode);
                _logger.LogInformation($"[AnnotationJob] Annotazione completata per {gameCode}");
            }
        }
    }
}
