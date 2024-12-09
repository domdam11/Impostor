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


namespace Impostor.Plugins.SemanticAnnotator.Annotator
{
    public static class EventUtility
    {
        private static DateTimeOffset TimeStamp { get;  set; }
        private static IGame Game { get;  set; }
        private static List<IEvent> Events;
        private static CowlWrapper annotator = new CowlWrapper();
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

        // start game
        public static void StartGame(IGame game)
        {
            if (Game is null || game.Code.Code != Game.Code.Code ) {
                //creato un nuovo gioco
                CreateGame(game);
            } else if (game.Code.Code == Game.Code.Code) {
                //gioco ripartito, incremento n_restarts in modo da non sovrascrivere
                var rest = NumRestarts + 1;
                CreateGame(game, rest);
            }
            GameStarted = true;
        }

        // end game
        public static void EndGame(DateTimeOffset currentTime, Boolean destroyed = false)
        {
            GameEnded = true;
            if (destroyed) {
                CallAnnotate(currentTime, true);
            } else {
               CallAnnotate(currentTime);   
            }
        }

        // set time
        public static void SetTime(DateTimeOffset timeStamp)
        {
            TimeStamp = timeStamp;
        }

        // Method to store event
        public static void SaveEvent(IEvent newEvent)
        {
            // Append the new event
            if (newEvent is IPlayerMovementEvent movEvent) {
                //handle movement event
                var movementStruct = new CustomPlayerMovementEvent(movEvent.Game, movEvent.ClientPlayer, movEvent.PlayerControl);
                Events.Add(movementStruct);
            } else {
                Events.Add(newEvent);
            }
        }

        public static void CallAnnotate(DateTimeOffset currentTime, Boolean destroyed=false)
        {
            if (GameStarted && Game != null) {
                //game started so annotaing makes sense
                if (Events.Count() != 0) {
                    //something to annotate
                    if ((currentTime - TimeStamp).TotalSeconds >= 3) {
                        //4s passed after last annotation  
                        var States = annotator.Annotate(Game, Events, PlayerStates, GameState, CallCount, NumRestarts); 
                        PlayerStates = States.Item1;
                        GameState = States.Item2; 
                        Events.Clear();
                        CallCount++;
                        TimeStamp = currentTime;
                    } else if (GameEnded) {
                        //annotate cause game is ended
                        annotator.Annotate(Game, Events, PlayerStates, GameState, CallCount, NumRestarts); 
                        //reset
                        CallCount=0;
                        Events.Clear();
                        if (destroyed) {
                            Game = null;
                            GameEnded = false;
                        }
                        PlayerStates = null;
                        GameStarted = false;
                        TimeStamp = currentTime;
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
                TimeStamp = currentTime;
            }
        }

    }

    public class PlayerStruct
    {
        public List<System.Numerics.Vector2> Movements { get; set; } = new List<System.Numerics.Vector2>(); // Lista dei movimenti
        public int VoteCount { get; set; } = 0; // Vote counter
        public string State { get; set; } = "none"; // player status 
    }

    public class CustomPlayerMovementEvent : IPlayerEvent
    {
        public IGame? Game { get; private set; }
        public IClientPlayer? ClientPlayer { get; private set; }
        public IInnerPlayerControl? PlayerControl { get; private set; }

        // Costruttore
        public CustomPlayerMovementEvent(IGame? game, IClientPlayer? clientPlayer, IInnerPlayerControl? playerControl)
        {
            Game = game;
            ClientPlayer = clientPlayer;
            PlayerControl = playerControl;
        }
    }

}

