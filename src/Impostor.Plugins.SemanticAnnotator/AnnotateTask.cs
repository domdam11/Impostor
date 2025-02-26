using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Coravel.Invocable;
using Impostor.Plugins.SemanticAnnotator;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.SemanticAnnotator
{
    /// <summary>
    /// Implements the IInvocable interface to manage periodic annotations.
    /// </summary>
    public class AnnotateTask : IInvocable
    {
        private readonly GameEventCacheManager _eventCacheManager;
        private readonly AnnotatorEngine _annotatorEngine;
        private readonly ILogger<AnnotateTask> _logger;

        /// <summary>
        /// Constructor with dependency injection.
        /// </summary>
        /// <param name="eventCacheManager">Manages the cached game events.</param>
        /// <param name="annotatorEngine">Processes game events and generates annotations.</param>
        /// <param name="logger">Handles logging for debugging and monitoring.</param>
        public AnnotateTask(GameEventCacheManager eventCacheManager, AnnotatorEngine annotatorEngine, ILogger<AnnotateTask> logger)
        {
            _eventCacheManager = eventCacheManager;
            _annotatorEngine = annotatorEngine;
            _logger = logger;
        }

        /// <summary>
        /// Asynchronous method for annotating game events of a session.
        /// </summary>
        /// <param name="gameCode">The game session code to annotate.</param>
        /// <returns>Asynchronous task.</returns>
        public async Task AnnotateAsync(string gameCode)
        {
            if (string.IsNullOrWhiteSpace(gameCode) || gameCode == "unassigned")
            {
                _logger.LogInformation($"Salta l'annotazione per GameCode: {gameCode}.");
                return;
            }
            _logger.LogInformation($"Inizio annotazione per GameCode: {gameCode}.");

            // Retrieve the game state from the cache using the GameEventCacheManager
            var gameState = await _eventCacheManager.GetGameStateAsync(gameCode);
            if (gameState == null)
            {
                _logger.LogWarning($"Stato del gioco non trovato per GameCode: {gameCode}.");
                return;
            }

            // Check if the game has ended to perform a final annotation
            bool isFinalAnnotation = gameState.GameEnded;

            // Pass the game state to the AnnotatorEngine for processing
            var (playerAnnotations, updatedGameStateName) = _annotatorEngine.Annotate(
                gameCode,
                _eventCacheManager,
                numAnnot: gameState.CallCount + 1,
                numRestarts: gameState.NumRestarts,
                timestamp: DateTimeOffset.UtcNow
            );

            // Update the count of performed annotations
            gameState.CallCount += 1;

            // Update the game state with the new state name
            gameState.GameStateName = updatedGameStateName;

            // If the game has ended, keep its state finalized
            if (isFinalAnnotation)
            {
                gameState.GameEnded = true;
            }

            // Update the game state in the cache
            await _eventCacheManager.UpdateGameStateAsync(gameCode, gameState);

            /// Clear the cached events after annotation is completed
            await _eventCacheManager.ClearGameEventsAsync(gameCode);

            // Log annotation completion
            _logger.LogInformation($"Annotazione completata per GameCode: {gameCode}.");
        }

        /// <summary>
        /// Invocable method used by Coravel to annotate all active sessions.
        /// </summary>
        /// <returns>Asynchronous task.</returns>
        public async Task Invoke()
        {
            // Retrieve all active game sessions
            var activeSessions = _eventCacheManager.GetActiveSessions();

            if (activeSessions.Count == 0)
            {
                _logger.LogInformation("Nessuna sessione attiva trovata per l'annotazione.");
                return;
            }

            _logger.LogInformation($"Annotazione avviata per {activeSessions.Count} sessioni attive.");

            foreach (var gameCode in activeSessions)
            {
                // Retrieve the game state for each active session
                var gameState = await _eventCacheManager.GetGameStateAsync(gameCode);
                if (gameState == null)
                {
                    _logger.LogWarning($"Stato del gioco non trovato per GameCode: {gameCode} durante l'annotazione.");
                    continue;
                }

                // If the game has ended, perform a final annotation and remove it from active cache
                if (gameState.GameEnded && !gameState.FinalAnnotationDone)
                {
                    _logger.LogInformation($"GameCode: {gameCode} è terminato. Annotazione finale eseguita.");
                    await AnnotateAsync(gameCode);
                    // Segnamo che abbiamo fatto la final annotation
                    gameState.FinalAnnotationDone = true;
                    await _eventCacheManager.UpdateGameStateAsync(gameCode, gameState);

                    continue;
                }
                if (gameState.IsInMatch)
                {
                    _logger.LogInformation($"[AnnotateTask] Eseguo annotazione periodica per match in corso: {gameCode}");
                    await AnnotateAsync(gameCode);
                }
            }

            _logger.LogInformation("Annotazione completata per tutte le sessioni attive.");
        }
    }
}
