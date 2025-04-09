using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Adapters
{
    public class AnnotatorAdapter : IAnnotator
    {
        private readonly AnnotatorEngine _annotatorEngine;
        private readonly GameEventCacheManager _eventCacheManager;
        private readonly ILogger<AnnotatorAdapter> _logger;

        public AnnotatorAdapter(
            AnnotatorEngine annotatorEngine,
            GameEventCacheManager eventCacheManager,
            ILogger<AnnotatorAdapter> logger)
        {
            _annotatorEngine = annotatorEngine;
            _eventCacheManager = eventCacheManager;
            _logger = logger;
        }

        public async Task AnnotateAsync(string gameCode)
        {
            if (string.IsNullOrWhiteSpace(gameCode) || gameCode == "unassigned")
            {
                _logger.LogInformation($"[AnnotatorAdapter] GameCode non valido: {gameCode}. Annotazione saltata.");
                return;
            }

            var gameState = await _eventCacheManager.GetGameStateAsync(gameCode);
            if (gameState == null)
            {
                _logger.LogWarning($"[AnnotatorAdapter] Stato del gioco non trovato per GameCode: {gameCode}.");
                return;
            }

            try
            {
                var (playerAnnotations, updatedGameStateName) = _annotatorEngine.Annotate(
                    gameCode,
                    _eventCacheManager,
                    numAnnot: gameState.CallCount + 1,
                    numRestarts: gameState.NumRestarts,
                    timestamp: DateTimeOffset.UtcNow
                );

                gameState.CallCount += 1;
                gameState.GameStateName = updatedGameStateName;

                if (gameState.GameEnded)
                {
                    gameState.GameEnded = true;
                }

                await _eventCacheManager.UpdateGameStateAsync(gameCode, gameState);
                await _eventCacheManager.ClearGameEventsAsync(gameCode);

                _logger.LogInformation($"[AnnotatorAdapter] Annotazione completata per GameCode: {gameCode}.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"[AnnotatorAdapter] Errore durante l'annotazione di {gameCode}");
            }
        }
    }
}
