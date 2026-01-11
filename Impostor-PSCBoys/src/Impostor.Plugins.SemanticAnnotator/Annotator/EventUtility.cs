using System;
using System.Collections.Generic;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Runtime.CompilerServices;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    public class EventUtility
    {

        public DateTimeOffset LastAnnotTimestamp { get; private set; }
        public DateTimeOffset CurrentTimestamp { get; private set; }
        public IGame Game { get; set; }
        public List<IEvent> Events;
        public List<IEvent> EventsOnlyNotarized;
        public Dictionary<byte, PlayerStruct> PlayerStates;
        public string GameState;

        public int CallCount;
        public int NumRestarts;
        public Boolean GameStarted;
        public Boolean GameEnded;

        public string? LastStrategy { get; private set; }

        public void SetLastStrategy(string strategy)
        {
            LastStrategy = strategy;
        }

        // define the game
        public void CreateGame(IGame game, int numRestarts = 0)
        {
            Game = game;
            Events = new List<IEvent>();
            EventsOnlyNotarized = new List<IEvent>();
            PlayerStates = new Dictionary<byte, PlayerStruct>();
            GameState = "none";
            NumRestarts = numRestarts;
            CallCount = 0;
            GameStarted = false;
            GameEnded = false;
        }

        public void CreatePlayers()
        {
            // players in current game
            var counter = 1;
            foreach (var p in Game.Players)
            {
                if (p == null) break;
                if (p.Character == null) break;
                // start tracking player state
                var mov = new CustomMovement(p.Character.NetworkTransform.Position, CurrentTimestamp);
                PlayerStruct playerStruct = new PlayerStruct
                {
                    State = "none",  // initial status
                    Movements = new List<CustomMovement> { mov }, // actual position                        
                    VoteCount = 0, // vote counter
                    SessionCls = $"Player{counter}" // session class
                };

                PlayerStates.Add(p.Character.PlayerId, playerStruct);
                counter++;
            }
        }

        // start game
        public void StartGame(IGame game)
        {
            if (Game is null || game.Code.Code != Game.Code.Code)
            {
                //create a new game
                CreateGame(game);
                //assign session class and more
                CreatePlayers();
            }
            else if (game.Code.Code == Game.Code.Code)
            {
                //game restarted
                var rest = NumRestarts + 1;
                CreateGame(game, rest);
                //assign session class and more
                CreatePlayers();
            }
            GameStarted = true;
        }

        /// <summary>
        /// Ends the current game session and resets the game state.
        /// </summary>
        /// <remarks>This method clears all game events, resets player states, and marks the game as not
        /// started.  If <paramref name="destroyed"/> is <see langword="true"/>, the game instance is set to <see
        /// langword="null"/>  and the restart count is reset to zero. The method is intended to be called when a game
        /// session ends,  either normally or due to an external interruption.</remarks>
        /// <param name="annotatorEngine">The annotator engine used to process game events. This parameter is not used in this method but is included
        /// for compatibility with related workflows.</param>
        /// <param name="destroyed">A boolean value indicating whether the game is being destroyed.  <see langword="true"/> resets all
        /// game-related data, including the game instance and restart count;  <see langword="false"/> retains certain
        /// state information for potential reuse.</param>
        public void CheckEndGame(IAnnotator annotatorEngine)
        {
            var gameEnded = EventsOnlyNotarized.Any(a => a is IGameEndedEvent);
            var gameDestroyed = EventsOnlyNotarized.Any(a => a is IGameDestroyedEvent);
            if (gameEnded || gameDestroyed)
            {
                GameEnded = true;
                CallCount = 0;
                Events.Clear();
                // Reset player states to initial values
                if (gameDestroyed)
                {
                    Game = null;
                    GameEnded = false;
                    NumRestarts = 0;
                }
                PlayerStates = null;
                GameStarted = false;
                LastAnnotTimestamp = CurrentTimestamp;
            }

        }
        /// <summary>
        /// 
        /// </summary>
        /// <param name="annotatorEngine"></param>
        /// <param name="destroyed"></param>
        /// <returns></returns>
        public AnnotationData CallAnnotate(AnnotatorEngine annotatorEngine)
        {
            AnnotationData annotationData = new AnnotationData();
            if (GameStarted && Game != null)
            {
                //game started so annotating makes sense
                if (Events.Count() != 0)
                {
                    //something to annotate
                    //if ((CurrentTimestamp - LastAnnotTimestamp).TotalSeconds >= totalSeconds)
                    {
                        //2s passed after last annotation  
                        var (playerStates, gameState, data) = annotatorEngine.Annotate(Game, 
                            Events, PlayerStates, GameState, CallCount, NumRestarts, CurrentTimestamp);
                        PlayerStates = playerStates;
                        GameState = gameState;
                        annotationData = data;
                        Events.Clear();
                        CallCount++;
                        LastAnnotTimestamp = CurrentTimestamp;
                    }
                    
                }
            }
            
            return annotationData;
        }

  
        public void SetAnnotationTime(DateTimeOffset timestamp)
        {
            LastAnnotTimestamp = timestamp;
        }
        public void SetCurrentTime(DateTimeOffset timestamp)
        {
            CurrentTimestamp = timestamp;
        }

        // Method to store event
        public void SaveEvent(IEvent newEvent)
        {
            // Append the new event
            if (newEvent is IPlayerMovementEvent movEvent)
            {
                //handle movement event
                var movementStruct = new CustomPlayerMovementEvent(movEvent.Game, movEvent.ClientPlayer, movEvent.PlayerControl, CurrentTimestamp);
                Events.Add(movementStruct);
            }
            else if(newEvent is IGameCreatedEvent || 
                newEvent is IGameStartedEvent || 
                newEvent is IGamePlayerLeftEvent || 
                newEvent is IGamePlayerJoinedEvent || 
                newEvent is IGameEndedEvent ||
                newEvent is IGameDestroyedEvent)
            {
                EventsOnlyNotarized.Add(newEvent);
            }
            else
            {
                Events.Add(newEvent);
            }
        }

    }



}
