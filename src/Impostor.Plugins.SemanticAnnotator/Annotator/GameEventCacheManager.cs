using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Plugins.SemanticAnnotator.Models;

namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    /// <summary>
    /// Manages the cache of game events, storing session states and player actions
    /// </summary>
    public class GameEventCacheManager
    {
        // Dictionary with gameCode as the key and GameState as the value
        private readonly Dictionary<string, EventUtility> _gameCache;

        /// <summary>
        /// Initializes the dictionary for caching game states.
        /// </summary>
        public GameEventCacheManager()
        {
            _gameCache = new Dictionary<string, EventUtility>();
        }

        public void SaveEvent(string gameCode, IEvent newEvent)
        {
            if(!_gameCache.ContainsKey(gameCode))
            {
                _gameCache.Add(gameCode, new EventUtility());

            }
            _gameCache[gameCode].SaveEvent(newEvent);
        }

        public void CreateGame(IGame game, int numRestarts = 0)
        {
            if (!_gameCache.ContainsKey(game.Code))
            {
                var eventUtil = new EventUtility()
                {
                    Game = game,
                    Events = new List<IEvent>(),
                    PlayerStates = new Dictionary<byte, PlayerStruct>(),
                    GameState = "none",
                    NumRestarts = 0,
                    CallCount = 0,
                    GameStarted = false,
                    GameEnded = false
                };
                _gameCache.Add(game.Code, eventUtil);

            }
            
        }

        public void StartGame(IGame game)
        {
            if (!_gameCache.ContainsKey(game.Code))
            {
                _gameCache.Add(game.Code, new EventUtility());

            }
            _gameCache[game.Code].StartGame(game);
        }

        /// <summary>
        /// Retrieves all active game sessions based on their GameCode.
        /// </summary>
        /// <returns>List of active session GameCodes.</returns>
        public List<string> GetActiveSessions()
        {
            // Returns only the keys (gameCodes) of active sessions
            return new List<string>(_gameCache.Where(a=>a.Key != null).Select(a=>a.Key).ToList());
        }

        public string CallAnnotate(string gameCode, AnnotatorEngine annotatorEngine, DateTime currentTimestamp, Boolean destroyed = false)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                return _gameCache[gameCode].CallAnnotate(annotatorEngine, currentTimestamp, destroyed);

            }
            return null;
        }

        public void EndGame(string gameCode, AnnotatorEngine annotatorEngine, DateTime currentTimestamp, Boolean destroyed = false)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                _gameCache[gameCode].EndGame(annotatorEngine, currentTimestamp, destroyed);

            }
        }

        public IEnumerable<IEvent> GetEventsByGameCodeAsync(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                return _gameCache[gameCode].Events;

            }
            else return Enumerable.Empty<IEvent>();
        }

        public bool IsInMatch(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                return _gameCache[gameCode].GameStarted;

            }
            else return false;
        }

        public string GetAnnotationEventId(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                return _gameCache[gameCode].Game.Code + "_" + _gameCache[gameCode].CallCount;

            }
            else return "";
        }
    }


}
