using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;

namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    /// <summary>
    /// Manages the cache of game events, storing session states and player actions
    /// </summary>
    public class GameEventCacheManager
    {
        // Dictionary with gameCode as the key and GameState as the value
        private readonly Dictionary<string, GameState> _gameCache;

        /// <summary>
        /// Initializes the dictionary for caching game states.
        /// </summary>
        public GameEventCacheManager()
        {
            _gameCache = new Dictionary<string, GameState>();
        }

        /// <summary>
        /// Retrieves all active game sessions based on their GameCode.
        /// </summary>
        /// <returns>List of active session GameCodes.</returns>
        public List<string> GetActiveSessions()
        {
            // Returns only the keys (gameCodes) of active sessions
            return new List<string>(_gameCache.Keys);
        }

        /// <summary>
        /// Adds a new game session to the cache.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <param name="gameState">Initial state of the game.</param>
        /// <returns>Asynchronous Task.</returns>
        public async Task AddGameAsync(string gameCode, GameState gameState)
        {
            if (gameCode != "unassigned")
            {
                // Adds the game to the cache (if it doesn't already exist)
                if (!_gameCache.ContainsKey(gameCode))
                    _gameCache[gameCode] = gameState;
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves the state of a specific game session.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <returns>Game state or null if not found.</returns>
        public async Task<GameState> GetGameStateAsync(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
                return await Task.FromResult(_gameCache[gameCode]);

            return null;
        }

        /// <summary>
        /// Updates the state of a specific game session.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <param name="gameState">New state of the game.</param>
        /// <returns>Asynchronous Task.</returns>
        public async Task UpdateGameStateAsync(string gameCode, GameState gameState)
        {
            if (_gameCache.ContainsKey(gameCode))
                _gameCache[gameCode] = gameState;

            await Task.CompletedTask;
        }

        /// <summary>
        /// Adds an event to a specific game session.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <param name="eventData">Event data.</param>
        /// <returns>Asynchronous Task.</returns>
        public async Task AddEventAsync(string gameCode, Dictionary<string, object> eventData)
        {
            if (!_gameCache.ContainsKey(gameCode))
                return;

            // Adds the event to the game history
            _gameCache[gameCode].EventHistory.Add(eventData);

            await Task.CompletedTask;
        }

        /// <summary>
        /// Retrieves all events for a specific game session.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <returns>List of events or an empty list if none exist.</returns>
        public async Task<List<Dictionary<string, object>>> GetEventsByGameCodeAsync(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                return await Task.FromResult(_gameCache[gameCode].EventHistory);
            }

            return await Task.FromResult(new List<Dictionary<string, object>>());
        }

        /// <summary>
        /// Retrieves all events for all games.
        /// </summary>
        /// <returns>Dictionary containing all events for each GameCode.</returns>
        public async Task<Dictionary<string, List<Dictionary<string, object>>>> GetAllEventsAsync()
        {
            // Creates a temporary dictionary to return the events
            var allEvents = _gameCache.ToDictionary(
                entry => entry.Key,
                entry => entry.Value.EventHistory
            );

            return await Task.FromResult(allEvents);
        }

        /// <summary>
        /// Adds or updates the state of a player within a game session.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <param name="playerState">Player state information.</param>
        /// <returns>Asynchronous Task.</returns>
        public async Task AddOrUpdatePlayerAsync(string gameCode, PlayerState playerState)
        {
            if (!_gameCache.ContainsKey(gameCode))
                return;

            var gameState = _gameCache[gameCode];
            var existingPlayer = gameState.Players.FirstOrDefault(p => p.Name.Equals(playerState.Name, StringComparison.OrdinalIgnoreCase));

            if (existingPlayer != null)
            {
                // Updates the existing player's state
                existingPlayer.Role = playerState.Role;
                existingPlayer.IsDead = playerState.IsDead;
                existingPlayer.State = playerState.State;
                existingPlayer.Movements = playerState.Movements;
                existingPlayer.VoteCount = playerState.VoteCount;
            }
            else
            {
                // Adds a new player to the game state
                gameState.Players.Add(playerState);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Removes a player from a specific game session.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <param name="playerName">Player name to be removed.</param>
        /// <returns>Asynchronous Task.</returns>
        public async Task RemovePlayerAsync(string gameCode, string playerName)
        {
            if (!_gameCache.ContainsKey(gameCode))
                return;

            var gameState = _gameCache[gameCode];
            var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(playerName, StringComparison.OrdinalIgnoreCase));

            if (player != null)
            {
                gameState.Players.Remove(player);
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Clears the event history for a specific game session.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <returns>Asynchronous Task.</returns>
        public async Task ClearGameEventsAsync(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                _gameCache[gameCode].EventHistory.Clear();
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// Creates or resets the state of a game session, including restarts and call count.
        /// </summary>
        /// <param name="gameCode">Game session code.</param>
        /// <param name="isRestart">Indicates whether it is a game restart.</param>
        /// <returns>Asynchronous Task.</returns>
        public async Task CreateOrResetGameAsync(string gameCode, bool isRestart = false)
        {
            if (!_gameCache.ContainsKey(gameCode))
            {
                _gameCache[gameCode] = new GameState(gameCode);
            }
            else if (isRestart)
            {
                _gameCache[gameCode].NumRestarts++;
                _gameCache[gameCode].Players.Clear();
                _gameCache[gameCode].EventHistory.Clear();
                _gameCache[gameCode].GameStateName = "lobby";
                _gameCache[gameCode].AlivePlayers = 0;
                _gameCache[gameCode].GameStarted = false;
                _gameCache[gameCode].GameEnded = false;
                _gameCache[gameCode].CallCount = 0;
            }

            await Task.CompletedTask;
        }


        

        


    }


}
