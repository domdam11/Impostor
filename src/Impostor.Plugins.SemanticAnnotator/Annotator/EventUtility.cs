using Impostor.Api.Games;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Plugins.SemanticAnnotator.Utils;
using System.Runtime.CompilerServices;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Api.Net.Inner.Objects;
using System.Diagnostics.Metrics;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    public static class EventUtility
    {
        private static DateTimeOffset LastAnnotTimestamp { get;  set; }
        private static DateTimeOffset CurrentTimestamp { get;  set; }
        private static IGame Game { get;  set; }
        private static List<IEvent> Events;
        private static AnnotatorEngine annotator = new AnnotatorEngine();
        private static Dictionary<byte, PlayerStruct> PlayerStates;
        private static string GameState;
    
        private static int CallCount;
        private static int NumRestarts;
        private static Boolean GameStarted;
        private static Boolean GameEnded;
        
        // define the game
        public static void CreateGame(IGame game, int numRestarts=0)
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

        public static void CreatePlayers()
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
        public static void StartGame(IGame game)
        {
            if (Game is null || game.Code.Code != Game.Code.Code ) {
                //create a new game
                CreateGame(game);
                //assign session class and more
                CreatePlayers();
            } else if (game.Code.Code == Game.Code.Code) {
                //game restarted
                var rest = NumRestarts + 1;
                CreateGame(game, rest);
                //assign session class and more
                CreatePlayers();
            }
            GameStarted = true;
        }

        // end game
        public static void EndGame(Boolean destroyed = false)
        {
            GameEnded = true;
            if (destroyed) {
                CallAnnotate(true);
            } else {
               CallAnnotate();   
            }
        }

        // set time
        public static void SetAnnotTime(DateTimeOffset timestamp)
        {
            LastAnnotTimestamp = timestamp;
        }
        public static void SetCurrentTime(DateTimeOffset timestamp)
        {
            CurrentTimestamp = timestamp;
        }

        // Method to store event
        public static void SaveEvent(IEvent newEvent)
        {
            // Append the new event
            if (newEvent is IPlayerMovementEvent movEvent) {
                //handle movement event
                var movementStruct = new CustomPlayerMovementEvent(movEvent.Game, movEvent.ClientPlayer, movEvent.PlayerControl, CurrentTimestamp);
                Events.Add(movementStruct);
            } else {
                Events.Add(newEvent);
            }
        }

        public static void CallAnnotate(Boolean destroyed=false)
        {
            if (GameStarted && Game != null) {
                //game started so annotating makes sense
                if (Events.Count() != 0) {
                    //something to annotate
                    if ((CurrentTimestamp - LastAnnotTimestamp).TotalSeconds >= 2.0) {
                        //2s passed after last annotation  
                        var States = annotator.Annotate(Game, Events, PlayerStates, GameState, CallCount, NumRestarts, CurrentTimestamp); 
                        PlayerStates = States.Item1;
                        GameState = States.Item2; 
                        Events.Clear();
                        CallCount++;
                        LastAnnotTimestamp = CurrentTimestamp;
                    } else if (GameEnded) {
                        //annotate cause game is ended
                        annotator.Annotate(Game, Events, PlayerStates, GameState, CallCount, NumRestarts, CurrentTimestamp); 
                    }
                }
            }
            if (GameEnded) {
                // if game ended (or destroyed) without starting or with invalid game (null), reset and don't annotate
                CallCount=0;
                Events.Clear();
                if (destroyed) {
                    Game = null;
                    GameEnded = false;
                    NumRestarts = 0;
                }
                PlayerStates = null;
                GameStarted = false;
                LastAnnotTimestamp = CurrentTimestamp;
            }
        }

    }

    public class PlayerStruct
    {
        public List<CustomMovement> Movements { get; set; } = new List<CustomMovement>(); // Lista dei movimenti
        public int VoteCount { get; set; } = 0; // Vote counter
        public string State { get; set; } = "none"; // player status
        public string SessionCls { get; set; } = "Player0"; // player status
    }

    public class CustomPlayerMovementEvent : IPlayerEvent
    {
        public IGame? Game { get; }
        public IClientPlayer? ClientPlayer { get; }
        public IInnerPlayerControl? PlayerControl { get; }
        public  System.Numerics.Vector2 Position { get; }
        public DateTimeOffset Timestamp { get;  set; }

        // Costruttore
        public CustomPlayerMovementEvent(IGame? game, IClientPlayer? clientPlayer, IInnerPlayerControl? playerControl, DateTimeOffset timestamp)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
            Position = playerControl.NetworkTransform.Position;
            Timestamp = timestamp;
        }
    }

}
