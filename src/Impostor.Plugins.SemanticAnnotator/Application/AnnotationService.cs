using System;
using System.Threading.Tasks;
using System.Linq;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.SemanticAnnotator.Application
{
    public class AnnotationService
    {
        private readonly IGameSessionProvider _sessionProvider;
        private readonly IAnnotator _annotator;
        private readonly GameEventCacheManager _eventCacheManager;
        private readonly ILogger<AnnotationService> _logger;

        public AnnotationService(
            IGameSessionProvider sessionProvider,
            IAnnotator annotator,
            GameEventCacheManager eventCacheManager,
            ILogger<AnnotationService> logger)
        {
            _sessionProvider = sessionProvider;
            _annotator = annotator;
            _eventCacheManager = eventCacheManager;
            _logger = logger;
        }

        public async Task AnnotateAllSessionsAsync()
        {
            var activeSessions = _sessionProvider.GetActiveSessions()?.ToList();

            if (activeSessions == null || activeSessions.Count == 0)
            {
                _logger.LogInformation("Nessuna sessione attiva trovata per l'annotazione.");
                return;
            }

            _logger.LogInformation($"Annotazione avviata per {activeSessions.Count} sessioni attive.");

            foreach (var gameCode in activeSessions)
            {
                var gameState = await _eventCacheManager.GetGameStateAsync(gameCode);

                if (gameState == null)
                {
                    _logger.LogWarning($"Stato del gioco non trovato per GameCode: {gameCode}.");
                    continue;
                }

                if (gameState.GameEnded && !gameState.FinalAnnotationDone)
                {
                    _logger.LogInformation($"GameCode: {gameCode} è terminato. Annotazione finale eseguita.");
                    await _annotator.AnnotateAsync(gameCode);
                    gameState.FinalAnnotationDone = true;
                    await _eventCacheManager.UpdateGameStateAsync(gameCode, gameState);
                    continue;
                }

                if (gameState.IsInMatch)
                {
                    _logger.LogInformation($"Eseguo annotazione periodica per match in corso: {gameCode}");
                    await _annotator.AnnotateAsync(gameCode);
                }
            }

            _logger.LogInformation("Annotazione completata per tutte le sessioni attive.");
        }
    }
}
