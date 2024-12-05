using Impostor.Api.Games;
using Impostor.Api.Events;
using Impostor.Api.Events.Meeting;
using Impostor.Api.Events.Player;
using Impostor.Api.Events.Ship;
using Impostor.Plugins.SemanticAnnotator.Utils;
using System.Runtime.CompilerServices;
using Impostor.Api.Innersloth;

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

        public static void StartGame(IGame game)
        {
            if (Game is null || game.Code != Game.Code ) {
                //creato un nuovo gioco
                CreateGame(game);
            } else if (game.Code == Game.Code) {
                //gioco ripartito, incremento n_restarts in modo da non sovrascrivere
                CreateGame(game, NumRestarts++);
            }
            GameStarted = true;
        }

        public static void EndGame(DateTimeOffset currentTime, Boolean destroyed = false)
        {
            if (Game is not null) {
                GameEnded = true;
                if (destroyed) {
                    CallAnnotate(currentTime, true);
                } else {
                    CallAnnotate(currentTime);
                }
                
            }
        }

        public static void SetTime(DateTimeOffset timeStamp)
        {
            TimeStamp = timeStamp;
        }

        // Method to store event
        public static void SaveEvent(IEvent newEvent)
        {
            if (Game is null) {
                //creo dizionario dove sono tutti none visto che game appena iniziato
                if (newEvent is IPlayerEvent playerEvent) { 
                    PlayerStates = playerEvent.Game.Players.ToDictionary(player => player.Character.PlayerId, player => new PlayerStruct());
                }
                else if(newEvent is IMeetingEvent meetingEvent) { 
                    PlayerStates = meetingEvent.Game.Players.ToDictionary(player => player.Character.PlayerId, player => new PlayerStruct());
                }
                else if(newEvent is IShipEvent shipEvent) { 
                    PlayerStates = shipEvent.Game.Players.ToDictionary(player => player.Character.PlayerId, player => new PlayerStruct());
                }
            }
            // Append the new event
            Events.Add(newEvent);
        }

        // Method to clear the JSON file if the time difference is greater than 5 seconds or game ended and call annotate
        public static void CallAnnotate(DateTimeOffset currentTime, Boolean destroyed=false)
        {
            if (GameStarted && Game != null) {
                //ha senso provare a scrivere perchè il gioco è iniziato
                if (Events.Count() != 0) {
                    //ho qualcosa da scrivere
                    if ((currentTime - TimeStamp).TotalSeconds >= 3) {
                        //scrivo perchè scaduto tempo finestra  
                        var States = annotator.Annotate(Game, Events, PlayerStates, GameState, CallCount, NumRestarts); 
                        PlayerStates = States.Item1;
                        GameState = States.Item2; 
                        Events.Clear();
                        CallCount++;
                    } else if (GameEnded) {
                        //scrivo perchè è finito il gioco
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
                    }
                }
            }
            if (GameEnded) {
                //se gioco terminato (distrutto se non iniziato) reset
                CallCount=0;
                Events.Clear();
                if (destroyed) {
                    Game = null;
                    GameEnded = false;
                }
                PlayerStates = null;
                GameStarted = false;
            }
            //in ogni caso
            TimeStamp = currentTime;
        }

    }

    public class PlayerStruct
    {
        public List<System.Numerics.Vector2> Movements { get; set; } = new List<System.Numerics.Vector2>(); // Lista dei movimenti
        public int VoteCount { get; set; } = 0; // Contatore dei voti
        public string State { get; set; } = "none"; // Stato del giocatore 
    }

}

