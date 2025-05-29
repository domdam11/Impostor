//using System;
//using System.Collections.Generic;
//using System.Linq;
//using System.Text;
//using System.Text.Json;
//using System.Threading.Tasks;

//namespace Impostor.Plugins.SemanticAnnotator.Models
//{
//    /// <summary>
//    /// Represents the state of a game session.
//    /// </summary>
//    public class GameState
//    {
//        public string GameCode { get; set; }
//        public string GameStateName { get; set; } // E.g. "meeting", "sabotage", "ended", etc.
//        public List<PlayerState> Players { get; set; } // List of player states
//        public List<Dictionary<string, object>> EventHistory { get; set; } // Game events history
//        public string Map { get; set; }
//        public int AlivePlayers { get; set; }
//        public string Host { get; set; } = "none";
//        public string GameOverReason { get; set; } = "";
//        public bool AnonymousVotesEnabled { get; set; }
//        public bool VisualTasksEnabled { get; set; }
//        public bool ConfirmEjects { get; set; }
//        // Tracking properties
//        public int NumRestarts { get; set; } = 0;
//        public int CallCount { get; set; } = 0; // Number of annotations made
//        public bool GameStarted { get; set; } = false;
//        public bool GameEnded { get; set; } = false;

//        public int MatchCounter { get; set; } = 0;
//        public bool IsInMatch { get; set; } = false;
//        public bool FinalAnnotationDone { get; set; } = false;

//        /// <summary>
//        /// Constructor to initialize a new game state.
//        /// </summary>
//        /// <param name="gameCode">Unique identifier for the game session.</param>
//        public GameState(string gameCode)
//        {
//            GameCode = gameCode;
//            GameStateName = "lobby";
//            Players = new List<PlayerState>();
//            EventHistory = new List<Dictionary<string, object>>();
//            Map = "UnknownMap";
//            AlivePlayers = 0;

//            MatchCounter = 0;
//            IsInMatch = false;
//            FinalAnnotationDone = false;
//        }

//        public string ToJson(bool includeEventHistory = true)
//        {
//            var options = new JsonSerializerOptions
//            {
//                WriteIndented = true
//            };

//            if (includeEventHistory)
//            {
//                return JsonSerializer.Serialize(this, options);
//            }
//            else
//            {
//                // Create a shallow copy with EventHistory null
//                var copy = new GameState(this.GameCode)
//                {
//                    GameStateName = this.GameStateName,
//                    Players = this.Players,
//                    EventHistory = null, // exclude
//                    Map = this.Map,
//                    AlivePlayers = this.AlivePlayers,
//                    Host = this.Host,
//                    GameOverReason = this.GameOverReason,
//                    AnonymousVotesEnabled = this.AnonymousVotesEnabled,
//                    VisualTasksEnabled = this.VisualTasksEnabled,
//                    ConfirmEjects = this.ConfirmEjects,
//                    NumRestarts = this.NumRestarts,
//                    CallCount = this.CallCount,
//                    GameStarted = this.GameStarted,
//                    GameEnded = this.GameEnded,
//                    MatchCounter = this.MatchCounter,
//                    IsInMatch = this.IsInMatch,
//                    FinalAnnotationDone = this.FinalAnnotationDone
//                };
//                return JsonSerializer.Serialize(copy, options);
//            }
//        }
//    }


//}
