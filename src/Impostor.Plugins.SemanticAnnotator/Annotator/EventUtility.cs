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
        private static Dictionary<byte, string> PlayerStates;
        private static string GameState;
        private static int CallCount = 0;

        public static void CreateGame(IGame game)
        {
            if (Game == null) {
                Game = game;
                Events = new List<IEvent>();
                PlayerStates = new Dictionary<byte, string>();
                GameState = "none";
            }
        }

        public static void SetTime(DateTimeOffset timeStamp)
        {
            TimeStamp = timeStamp;
        }

        // Method to store event
        public static void SaveEvent(IEvent newEvent)
        {
            if (Game == null) {
                //creo dizionario dove sono tutti none visto che game appena iniziato
                if (newEvent is IPlayerEvent playerEvent) { 
                    PlayerStates = playerEvent.Game.Players.ToDictionary(player => player.Character.PlayerId, player => "none");
                }
                else if(newEvent is IMeetingEvent meetingEvent) { 
                    PlayerStates = meetingEvent.Game.Players.ToDictionary(player => player.Character.PlayerId, player => "none");
                }
                else if(newEvent is IShipEvent shipEvent) { 
                    PlayerStates = shipEvent.Game.Players.ToDictionary(player => player.Character.PlayerId, player => "none");
                }
            }
            // Append the new event
            Events.Add(newEvent);
        }

        // Method to clear the JSON file if the time difference is greater than 5 seconds or game ended and call annotate
        public static void CallAnnotate(DateTimeOffset currentTime, Boolean gameEnd=false)
        {
            if (gameEnd && Game != null) 
            {
                var States = annotator.Annotate(Game, Events, PlayerStates, GameState, CallCount); 
                CallCount=0;
                Game = null;
                PlayerStates = null;
                GameState = null;
                Events.Clear();
            }
            // Check if the time difference exceeds 3 seconds
            else if (Events.Count() != 0 &&(currentTime - TimeStamp).TotalSeconds >= 10 && Game != null)
            {
                // If more than 5 seconds, empty the list 
                var States = annotator.Annotate(Game, Events, PlayerStates, GameState, CallCount); 
                PlayerStates = States.Item1;
                GameState = States.Item2;
                CallCount++;
                TimeStamp = currentTime;
                Events.Clear();
            }
        }

    }
}