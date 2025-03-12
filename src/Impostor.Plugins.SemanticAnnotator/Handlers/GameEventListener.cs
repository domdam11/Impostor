using Impostor.Api.Events;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Plugins.SemanticAnnotator;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using System.Linq;
using System.Collections.Generic;
using Impostor.Plugins.SemanticAnnotator.Models;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class GameEventListener : IEventListener
    {
        private readonly ILogger<GameEventListener> _logger;
        private readonly GameEventCacheManager _eventCacheManager;

        public GameEventListener(ILogger<GameEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        /// <summary>
        /// Handles the event when a game creation request is made.
        /// </summary>
        [EventListener]
        public async ValueTask OnGameCreated(IGameCreationEvent e)
        {
            _logger.LogInformation("Game creation requested by {client}", e.Client == null ? "a plugin" : e.Client.Name);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },   // Unique identifier for the event
                { "GameCode", "unassigned" },               // Default game code (to be assigned later)
                { "EventType", "GameCreationRequested" },   // Type of event
                { "Timestamp", DateTime.UtcNow },           // Event timestamp
                { "ClientName", e.Client?.Name ?? "a plugin" } // Name of the client requesting the game
            };

            // Check if the game state already exists in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync("unassigned");
            if (gameState == null)
            {
                // Initialize a new game state if none exists
                gameState = new GameState("unassigned");
                gameState.GameStateName = "created";
                await _eventCacheManager.AddGameAsync("unassigned", gameState);
            }
            // Store the event data in the cache
            await _eventCacheManager.AddEventAsync("unassigned", eventData);
        }

        /// <summary>
        /// Handles the event when a game is successfully created.
        /// </summary>
        [EventListener]
        public async ValueTask OnGameCreated(IGameCreatedEvent e)
        {
            _logger.LogInformation("Game {code} > created", e.Game.Code);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code.ToString() },
                { "EventType", "GameCreated" },
                { "Timestamp", DateTime.UtcNow },
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code.ToString(), eventData);

            // Update the game state in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState == null)
            {
                gameState = new GameState(e.Game.Code.ToString());
                gameState.GameStateName = "created";
                await _eventCacheManager.AddGameAsync(e.Game.Code.ToString(), gameState);
            }
            else
            {
                gameState.GameStateName = "created";
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
            }

            // Create or reset the game state
            await _eventCacheManager.CreateOrResetGameAsync(e.Game.Code.ToString(), isRestart: false);
        }

        /// <summary>
        /// Handles the event when a game is starting.
        /// </summary>
        [EventListener]
        public async ValueTask OnGameStarting(IGameStartingEvent e)
        {
            _logger.LogInformation("Game {code} > starting", e.Game.Code);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code.ToString() },
                { "EventType", "GameStarting" },
                { "Timestamp", DateTime.UtcNow },
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code.ToString(), eventData);

            // Update the game state in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState != null)
            {
                gameState.GameStateName = "starting";
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
            }
        }

        /// <summary>
        /// Handles the event when a game starts.
        /// </summary>
        [EventListener]
        public async ValueTask OnGameStarted(IGameStartedEvent e)
        {
            CsvUtility.CsvGeneratorStartGame(e.Game.Code, CsvUtility.TimeStamp.ToUnixTimeMilliseconds().ToString());
            _logger.LogInformation("Game {code} > started", e.Game.Code);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code.ToString() },
                { "EventType", "GameStarted" },
                { "Timestamp", DateTime.UtcNow }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code.ToString(), eventData);

            // Log all players and their roles
            foreach (var player in e.Game.Players)
            {
                var info = player.Character!.PlayerInfo;
                _logger.LogInformation("- {player} is {role}", info.PlayerName, info.RoleType);
            }

            // Update the game state in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState != null)
            {
                gameState.GameStateName = "started";
                gameState.AlivePlayers = e.Game.Players.Count(p => !p.Character.PlayerInfo.IsDead);
                gameState.Map = e.Game.Options.Map.ToString();
                gameState.GameStarted = true;
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
            }

            if (!gameState.IsInMatch)
            {
                gameState.MatchCounter++;      
                gameState.IsInMatch = true;
                gameState.GameEnded = false;
                gameState.GameStarted = true;
                gameState.FinalAnnotationDone = false;
            }

            await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
        }

        /// <summary>
        /// Handles the event when a game ends.
        /// </summary>
        [EventListener]
        public async ValueTask OnGameEnded(IGameEndedEvent e)
        {
            CsvUtility.CsvGeneratorEndGame(e.Game.Code, CsvUtility.TimeStamp.ToUnixTimeMilliseconds().ToString());
            _logger.LogInformation("Game {code} > ended because {reason}", e.Game.Code, e.GameOverReason);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code.ToString() },
                { "EventType", "GameEnded" },
                { "Timestamp", DateTime.UtcNow },
                { "GameOverReason", e.GameOverReason.ToString()}
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code.ToString(), eventData);

            // Update the game state in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState != null)
            {
                gameState.GameStateName = "ended";
                gameState.GameOverReason = e.GameOverReason.ToString();
                gameState.GameEnded = true;
                gameState.IsInMatch = false;
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
            }
        }

        /// <summary>
        /// Handles the event when a game is destroyed.
        /// </summary>
        [EventListener]
        public async ValueTask OnGameDestroyed(IGameDestroyedEvent e)
        {
            _logger.LogInformation("Game {code} > destroyed", e.Game.Code);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code.ToString() },   // Game code
                { "EventType", "GameDestroyed" },         // Type of event
                { "Timestamp", DateTime.UtcNow }          // Event timestamp
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code.ToString(), eventData);

            // Update the game state in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState != null)
            {
                gameState.GameStateName = "destroyed";
                gameState.GameEnded = true;
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
            }
        }

        /// <summary>
        /// Handles the event when the game host changes.
        /// </summary>
        [EventListener]
        public async ValueTask OnGameHostChanged(IGameHostChangedEvent e)
        {
            _logger.LogInformation(
                "Game {code} > changed host from {previous} to {new}",
                e.Game.Code,
                e.PreviousHost.Character?.PlayerInfo.PlayerName,
                e.NewHost != null ? e.NewHost.Character?.PlayerInfo.PlayerName : "none"
            );

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code.ToString() },   // Game code
                { "EventType", "GameHostChanged" },       // Type of event
                { "Timestamp", DateTime.UtcNow },         // Event timestamp
                { "PreviousHost", e.PreviousHost.Character?.PlayerInfo.PlayerName ?? "none" }, // Previous host name
                { "NewHost", e.NewHost?.Character?.PlayerInfo.PlayerName ?? "none" }  // New host name
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code.ToString(), eventData);

            // Update the game host in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState != null)
            {
                gameState.Host = e.NewHost?.Character?.PlayerInfo.PlayerName ?? "none";
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
            }
        }

        /// <summary>
        /// Handles the event when game options change.
        /// </summary>
        [EventListener]
        public async ValueTask OnGameOptionsChanged(IGameOptionsChangedEvent e)
        {
            _logger.LogInformation(
                "Game {code} > new options because of {source}",
                e.Game.Code,
                e.ChangedBy
            );

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code.ToString() },   // Game code
                { "EventType", "GameOptionsChanged" },   // Type of event
                { "Timestamp", DateTime.UtcNow },        // Event timestamp
                { "ChangedBy", e.ChangedBy }             // Source of change
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code.ToString(), eventData);

            // Update game options in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState != null)
            {
                // Update specific game options
                if (e.Game.Options is NormalGameOptions normalGameOptions)
                {
                    gameState.Map = normalGameOptions.Map.ToString();
                    gameState.AnonymousVotesEnabled = normalGameOptions.AnonymousVotes;
                    gameState.VisualTasksEnabled = normalGameOptions.VisualTasks;
                    gameState.ConfirmEjects = normalGameOptions.ConfirmImpostor;
                }
                else if (e.Game.Options is LegacyGameOptionsData legacyGameOptionsData)
                {
                    gameState.Map = legacyGameOptionsData.Map.ToString();
                    gameState.AnonymousVotesEnabled = legacyGameOptionsData.AnonymousVotes;
                    gameState.VisualTasksEnabled = legacyGameOptionsData.VisualTasks;
                    gameState.ConfirmEjects = legacyGameOptionsData.ConfirmImpostor;
                }

                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
            }
        }

        /// <summary>
        /// Handles the event when a player joins a game.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerJoined(IGamePlayerJoinedEvent e)
        {
            _logger.LogInformation("Game {code} > {player} joined", e.Game.Code, e.Player.Client.Name);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code.ToString() },   // Game code
                { "EventType", "PlayerJoined" },         // Type of event
                { "Timestamp", DateTime.UtcNow },        // Event timestamp
                { "Player", e.Player.Client.Name }       // Player name
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code.ToString(), eventData);

            // Update the player state in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.Player.Client.Name, StringComparison.OrdinalIgnoreCase));
                if (player == null)
                {
                    // Add a new player to the game state
                    var newPlayerState = new PlayerState(e.Player.Client.Name, e.Player.Character?.PlayerInfo?.RoleType?.ToString() ?? "Unknown")
                    {
                        Movements = new List<CustomMovement>(),
                        State = "alive",
                        VoteCount = 0
                    };
                    gameState.Players.Add(newPlayerState);
                }
                else
                {
                    // Update player status
                    player.State = "alive";
                }
            }

            // Save the updated state in the cache
            await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
        }

        /// <summary>
        /// Handles the event when a player leaves a game.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerLeftGame(IGamePlayerLeftEvent e)
        {
            _logger.LogInformation("Game {code} > {player} left", e.Game.Code, e.Player.Client.Name);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code.ToString() },   // Game code
                { "EventType", "PlayerLeftGame" },       // Type of event
                { "Timestamp", DateTime.UtcNow },        // Event timestamp
                { "Player", e.Player.Client.Name }       // Player name
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);

            // Update the player state in the cache
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code.ToString());
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.Player.Client.Name, StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    player.State = "left";
                }
            }

            // Save the updated state in the cache
            await _eventCacheManager.UpdateGameStateAsync(e.Game.Code.ToString(), gameState);
        }
    }
}
