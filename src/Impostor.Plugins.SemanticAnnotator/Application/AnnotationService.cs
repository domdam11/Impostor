using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Ports;
using System.Threading.Tasks;
using System;

namespace Impostor.Plugins.SemanticAnnotator.Application
{
    public class AnnotatorService : IAnnotator
    {
        private readonly AnnotatorEngine _engine;
        private readonly GameEventCacheManager _cacheManager;
        private readonly IAnnotationBuffer _buffer;

        public AnnotatorService(AnnotatorEngine engine, GameEventCacheManager cacheManager, IAnnotationBuffer buffer)
        {
            _engine = engine;
            _cacheManager = cacheManager;
            _buffer = buffer;
        }

        public async Task AnnotateAsync(string gameCode)
        {
            if (string.IsNullOrWhiteSpace(gameCode) || gameCode == "unassigned")
                return;

            var gameState = await _cacheManager.GetGameStateAsync(gameCode);
            if (gameState == null)
                return;

            var (results, newGameStateName) = _engine.Annotate(
                gameCode,
                _cacheManager,
                gameState.CallCount + 1,
                gameState.NumRestarts,
                DateTimeOffset.UtcNow
            );

            string owl = _engine.GetLastOwl();
            _buffer.Save(gameCode, owl);

            gameState.CallCount += 1;
            gameState.GameStateName = newGameStateName;
            await _cacheManager.UpdateGameStateAsync(gameCode, gameState);
            await _cacheManager.ClearGameEventsAsync(gameCode);
        }
    }
}
