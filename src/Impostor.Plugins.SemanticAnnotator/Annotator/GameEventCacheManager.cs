using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Utils;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    /// <summary>
    /// Manages the cache of game events, storing session states and player actions
    /// </summary>
    public class GameEventCacheManager
    {
        // Dictionary with gameCode as the key and GameState as the value
        private readonly Dictionary<string, EventUtility> _gameCache;
        private readonly IDateTimeProvider _dateTimeService;

        /// <summary>
        /// Initializes the dictionary for caching game states.
        /// </summary>
        public GameEventCacheManager(IDateTimeProvider dateTimeService)
        {
            _gameCache = new Dictionary<string, EventUtility>();
            _dateTimeService = dateTimeService;
        }

        public void SaveEvent(string gameCode, IEvent newEvent)
        {
            if(!_gameCache.ContainsKey(gameCode))
            {
                _gameCache.Add(gameCode, new EventUtility());

            }
            _gameCache[gameCode].SetCurrentTime(_dateTimeService.UtcNow);
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
                    EventsOnlyNotarized = new List<IEvent>(),
                    PlayerStates = new Dictionary<byte, PlayerStruct>(),
                    GameState = "none",
                    NumRestarts = 0,
                    CallCount = 0,
                    GameStarted = false,
                    GameEnded = false
                };
                _gameCache.Add(game.Code, eventUtil);
                _gameCache[game.Code].SetCurrentTime(_dateTimeService.UtcNow);
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

        public string CallAnnotate(string gameCode, AnnotatorEngine annotatorEngine)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                _gameCache[gameCode].SetCurrentTime(_dateTimeService.UtcNow);
                return _gameCache[gameCode].CallAnnotate(annotatorEngine);                
            }
            return null;
        }

        public IEnumerable<IEvent> GetEventsByGameCodeAsync(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                return _gameCache[gameCode].Events.ToList();

            }
            else return Enumerable.Empty<IEvent>();
        }

        public IEnumerable<IEvent> GetEventsOnlyNotarizedByGameCodeAsync(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                return _gameCache[gameCode].EventsOnlyNotarized.ToList();

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

        public IEnumerable<IClientPlayer> GetPlayerList(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {

                return _gameCache[gameCode].Game?.Players?.ToList();

            }
            else return new List<IClientPlayer>();
        }

        public string GetAnnotationEventId(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                var gameSessionId = GetGameSessionUniqueId(gameCode);
                return gameSessionId + "_" + _gameCache[gameCode].CallCount;

            }
            else return "";
        }

        public string GetGameSessionUniqueId(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode) && _gameCache[gameCode].Game != null)
            {
                return _gameCache[gameCode].Game.Code+"_"+ _gameCache[gameCode].NumRestarts;

            }
            else return null;
        }

        internal void ClearEventsOnlyNotarizedByGameCodeAsync(string gameCode)
        {
            if (_gameCache.ContainsKey(gameCode))
            {
                _gameCache[gameCode].EventsOnlyNotarized.Clear();
            }
      
        }

        internal void CheckEndGame(IGame game, IAnnotator annotatorEngine)
        {
            if (_gameCache.ContainsKey(game.Code)) { 
                _gameCache[game.Code].CheckEndGame(annotatorEngine);
            }
        }


    }


}
