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

        public DateTimeOffset LastAnnotTimestamp { get; set; }
        public DateTimeOffset CurrentTimestamp { get; set; }
        public IGame Game { get; set; }
        public List<IEvent> Events;
        public Dictionary<byte, PlayerStruct> PlayerStates;
        public string GameState;

        public int CallCount;
        public int NumRestarts;
        public Boolean GameStarted;
        public Boolean GameEnded;

        // define the game
        public void CreateGame(IGame game, int numRestarts = 0)
        {
            Game = game;
            Events = new List<IEvent>();
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

        // end game
        public void EndGame(AnnotatorEngine annotatorEngine, long totalSeconds, Boolean destroyed = false)
        {
            GameEnded = true;
            if (destroyed)
            {
                CallAnnotate(annotatorEngine, totalSeconds, true);
            }
            else
            {
                CallAnnotate(annotatorEngine, totalSeconds);
            }
        }

        public string CallAnnotate(AnnotatorEngine annotatorEngine, long totalSeconds, Boolean destroyed = false)
        {
            string owl = null;
            if (GameStarted && Game != null)
            {
                //game started so annotating makes sense
                if (Events.Count() != 0)
                {
                    //something to annotate
                    if ((CurrentTimestamp - LastAnnotTimestamp).TotalSeconds >= totalSeconds)
                    {
                        //2s passed after last annotation  
                        var (playerStates, gameState, owlOutput) = annotatorEngine.Annotate(Game, Events, PlayerStates, GameState, CallCount, NumRestarts, CurrentTimestamp);
                        PlayerStates = playerStates;
                        GameState = gameState;
                        owl = owlOutput;
                        Events.Clear();
                        CallCount++;
                        LastAnnotTimestamp = CurrentTimestamp;
                        return owl;
                    }
                    else if (GameEnded)
                    {
                        //annotate cause game is ended
                        annotatorEngine.Annotate(Game, Events, PlayerStates, GameState, CallCount, NumRestarts, CurrentTimestamp);
                    }
                }
            }
            if (GameEnded)
            {
                // if game ended (or destroyed) without starting or with invalid game (null), reset and don't annotate
                CallCount = 0;
                Events.Clear();
                if (destroyed)
                {
                    Game = null;
                    GameEnded = false;
                    NumRestarts = 0;
                }
                PlayerStates = null;
                GameStarted = false;
                LastAnnotTimestamp = CurrentTimestamp;
            }
            return owl;
        }

        // set time
        public void SetAnnotTime(DateTimeOffset timestamp)
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
            else
            {
                Events.Add(newEvent);
            }
        }

    }



}
