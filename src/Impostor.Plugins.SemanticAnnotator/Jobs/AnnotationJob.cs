using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Coravel.Invocable;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class AnnotationJob : IInvocable
    {
        private readonly GameEventCacheManager _cache;
        private readonly IAnnotationBuffer _buffer;
        private readonly AnnotatorEngine _engine;
        private readonly ILogger<AnnotationJob> _logger;

        public AnnotationJob(GameEventCacheManager cache, IAnnotationBuffer buffer, AnnotatorEngine engine, ILogger<AnnotationJob> logger)
        {
            _cache = cache;
            _buffer = buffer;
            _engine = engine;
            _logger = logger;
        }

        public Task Invoke()
        {
            foreach (var gameCode in _cache.GetActiveSessions())
            {
                var (result, _) = _engine.Annotate(gameCode, _cache, 0, 0, DateTimeOffset.UtcNow);
                string owl = _engine.GetLastOwl(); // Da implementare
                _buffer.Save(gameCode, owl);
                _logger.LogInformation($"[AnnotationJob] OWL generato e salvato per {gameCode}");
            }
            return Task.CompletedTask;
        }
    }
}
