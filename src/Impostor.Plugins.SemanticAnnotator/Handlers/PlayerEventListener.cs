using System;
using System.Linq;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Innersloth.Customization;
using Microsoft.Extensions.Logging;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Plugins.SemanticAnnotator;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Api.Net.Inner.Objects;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class PlayerEventListener : IEventListener
    {
        private readonly ILogger<PlayerEventListener> _logger;
        private readonly GameEventCacheManager _eventCacheManager;

        // Internal state to track player movements
        private readonly Dictionary<string, (Vector2 lastPosition, DateTime lastTimestamp)> _playerMovementTracker = new();

        public PlayerEventListener(ILogger<PlayerEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        /// <summary>
        /// Handles the event when a player spawns in the game.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            _logger.LogInformation("Player {player} > spawned", e.PlayerControl.PlayerInfo.PlayerName);

            // Create event data for player spawn
            var spawnEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique event identifier
                { "GameCode", e.Game.Code },              // Associated game code
                { "EventType", "PlayerSpawned" },         // Type of event
                { "Timestamp", DateTime.UtcNow },         // Time of the event
                { "PlayerId", e.PlayerControl.PlayerId }, // Player ID
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "") }, // Cleaned player name
                { "PlayerRole", e.PlayerControl.PlayerInfo.IsImpostor ? "Impostor" : "Crewmate"}, // Player role
            };

            // Save the event in the cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, spawnEventData);

            // Update the player state in the GameState
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (player == null)
                {
                    // Add a new player to the game state
                    var newPlayerState = new GameEventCacheManager.PlayerState(e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), e.PlayerControl.PlayerInfo.IsImpostor ? "Impostor" : "Crewmate")
                    {
                        Movements = new List<GameEventCacheManager.CustomMovement>
                        {

                            new GameEventCacheManager.CustomMovement(e.PlayerControl.NetworkTransform.Position, DateTimeOffset.UtcNow)
                        },
                        State = "alive",
                        VoteCount = 0
                    };
                    gameState.Players.Add(newPlayerState);
                }
                else
                {
                    // Update existing player information
                    player.State = "alive";
                    player.Role = e.PlayerControl.PlayerInfo.IsImpostor ? "Impostor" : "Crewmate";
                    player.Movements.Add(new GameEventCacheManager.CustomMovement(e.PlayerControl.NetworkTransform.Position, DateTimeOffset.UtcNow));
                }

                // Set the game started flag if not already set
                if (!gameState.GameStarted)
                {
                    gameState.GameStarted = true;
                    _logger.LogInformation($"GameCode: {e.Game.Code} ha iniziato.");
                }

                // Update the game state in the cache
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
            }

        }

        /// <summary>
        /// Handles player movement events, tracking position changes and time spent stationary.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerMovement(IPlayerMovementEvent e)
        {
            var currentPosition = e.PlayerControl.NetworkTransform.Position;

            // Retrieve previous movement data, if available
            if (_playerMovementTracker.TryGetValue(e.PlayerControl.PlayerId.ToString(), out var lastData))
            {
                var (lastPosition, lastTimestamp) = lastData;
                var timeSpent = (DateTime.UtcNow - lastTimestamp).TotalSeconds;

                // Log player movement or stationary time
                if (lastPosition == currentPosition)
                {
                    _logger.LogDebug("Player {playerId} ({playerName}) stationary at {position} for {timeSpent:F1} seconds.",
                        e.PlayerControl.PlayerId, e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), currentPosition, timeSpent);
                }
                else
                {
                    _logger.LogDebug("Player {playerId} ({playerName}) moved from {lastPosition} to {currentPosition}.",
                        e.PlayerControl.PlayerId, e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), lastPosition, currentPosition);
                }

                // Create movement event data
                var movementEventData = new Dictionary<string, object>
                {
                    { "EventId", Guid.NewGuid().ToString() },
                    { "GameCode", e.Game.Code },
                    { "EventType", "PlayerMovement" },
                    { "Timestamp", DateTime.UtcNow },
                    { "PlayerId", e.PlayerControl.PlayerId },
                    { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "") },
                    { "PreviousPositionX", lastPosition.X },
                    { "PreviousPositionY", lastPosition.Y },
                    { "CurrentPositionX", currentPosition.X },
                    { "CurrentPositionY", currentPosition.Y },
                    { "TimeSpentStationary", timeSpent }
                };

                await _eventCacheManager.AddEventAsync(e.Game.Code, movementEventData);
            }

            // Update the player's movement state
            _playerMovementTracker[e.PlayerControl.PlayerId.ToString()] = (currentPosition, DateTime.UtcNow);

            // Update the player movement in GameState
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    // Add the current movement to player history
                    player.Movements.Add(new GameEventCacheManager.CustomMovement(currentPosition, DateTimeOffset.UtcNow));

                    await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                }
            }
        }

        /// <summary>
        /// Handles the event when a player is destroyed (killed or disconnected).
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerDestroyed(IPlayerDestroyedEvent e)
        {
            _logger.LogInformation("Player {player} > destroyed", e.PlayerControl.PlayerInfo.PlayerName);

            // Create event data for player destruction
            var destroyEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "PlayerDestroyed" },
                { "Timestamp", DateTime.UtcNow },
                { "PlayerId", e.PlayerControl.PlayerId },
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "")},
                { "DeathReason", e.PlayerControl.PlayerInfo.LastDeathReason.ToString() }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, destroyEventData);

            // Update the player's state in GameState
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    player.State = "dead";
                    await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                }
                // Check if the game has ended (e.g., all impostors or all crewmates are dead)
                bool allImpostorsDead = gameState.Players.Where(p => p.Role == "Impostor").All(p => p.State.Equals("dead", StringComparison.OrdinalIgnoreCase));
                bool allCrewmatesDead = gameState.Players.Where(p => p.Role == "Crewmate").All(p => p.State.Equals("dead", StringComparison.OrdinalIgnoreCase));

                if (allImpostorsDead || allCrewmatesDead)
                {
                    gameState.GameEnded = true;
                    gameState.GameStateName = "ended";
                    gameState.GameOverReason = allImpostorsDead ? "CrewmatesWin" : "ImpostorsWin";
                    _logger.LogInformation($"GameCode: {e.Game.Code} è terminato. Ragioni: {gameState.GameOverReason}");
                    await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                }
            }
        }

        /// <summary>
        /// Handles the event when a player sends a chat message.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerChatAsync(IPlayerChatEvent e)
        {
            _logger.LogInformation("Player {player} > said {message}", e.PlayerControl.PlayerInfo.PlayerName, e.Message);

            // Create a dictionary to store chat event data
            var chatEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code },               // Game code
                { "EventType", "PlayerChat" },             // Type of event
                { "Timestamp", DateTime.UtcNow },          // Event timestamp
                { "PlayerId", e.PlayerControl.PlayerId },  // Player ID
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "")}, // Player name without spaces
                { "Message", e.Message }                   // Chat message content
            };

            // Save the event in the cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, chatEventData);

            // Respond to specific chat commands
            if (e.Message == "test")
            {
                e.Game.Options.NumImpostors = 2;

                if (e.Game.Options is NormalGameOptions normalGameOptions)
                {
                    normalGameOptions.KillCooldown = 0;
                    normalGameOptions.PlayerSpeedMod = 5;
                }

                await e.Game.SyncSettingsAsync();
            }

            if (e.Message == "look")
            {
                await e.PlayerControl.SetColorAsync(ColorType.Pink);
                await e.PlayerControl.SetHatAsync("hat_pk05_Cheese");
                await e.PlayerControl.SetSkinAsync("skin_Police");
                await e.PlayerControl.SetPetAsync("pet_alien1");
            }

            if (e.Message == "snap")
            {
                await e.PlayerControl.NetworkTransform.SnapToAsync(new Vector2(1, 1));
            }

            if (e.Message == "completetasks")
            {
                foreach (var task in e.PlayerControl.PlayerInfo.Tasks)
                {
                    await task.CompleteAsync();
                }
            }

            await e.PlayerControl.SendChatAsync(e.Message);
        }

        /// <summary>
        /// Handles the event when a player starts a meeting (either emergency meeting or reporting a dead body).
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerStartMeetingEvent(IPlayerStartMeetingEvent e)
        {
            _logger.LogInformation("Player {player} > started meeting, reason: {reason}", e.PlayerControl.PlayerInfo.PlayerName, e.Body == null ? "Emergency call button" : "Found the body of the player " + e.Body.PlayerInfo.PlayerName);

            // Create a dictionary to store meeting start event data
            var meetingStartEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code },               // Game code
                { "EventType", "PlayerStartMeeting" },     // Type of event
                { "Timestamp", DateTime.UtcNow },          // Event timestamp
                { "PlayerId", e.PlayerControl.PlayerId },  // Player ID
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "")}, // Player name
                { "Reason", e.Body == null ? "Emergency call button" : "Found the body of player " + e.Body.PlayerInfo.PlayerName }
            };

            // Save the event in the cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, meetingStartEventData);

            // Update the game state to "meeting"
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                gameState.GameStateName = "meeting";
                await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
            }
        }

        /// <summary>
        /// Handles the event when a player enters a vent.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerEnterVentEvent(IPlayerEnterVentEvent e)
        {
            _logger.LogInformation("Player {player} entered the vent in {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.Vent.Name, e.Vent.Id);

            // Create a dictionary to store vent entry event data
            var playerEnterVentEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code },               // Game code
                { "EventType", "PlayerEnterVentEvent" },   // Type of event
                { "Timestamp", DateTime.UtcNow },          // Event timestamp
                { "PlayerId", e.PlayerControl.PlayerId },  // Player ID
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "")},
                { "VentName", e.Vent.Name },               // Vent name
                { "VentId", e.Vent.Id },                   // Vent ID
                { "PositionX", e.PlayerControl.NetworkTransform.Position.X },
                { "PositionY", e.PlayerControl.NetworkTransform.Position.Y }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, playerEnterVentEventData);

            // Update the player state in the GameState
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    // Update player state to "venting"
                    player.State = "venting";
                    player.Movements.Add(new GameEventCacheManager.CustomMovement(e.PlayerControl.NetworkTransform.Position, DateTimeOffset.UtcNow));
                    await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                }
            }
        }

        /// <summary>
        /// Handles the event when a player exits a vent.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerExitVentEvent(IPlayerExitVentEvent e)
        {
            _logger.LogInformation("Player {player} exited the vent in {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.Vent.Name, e.Vent.Id);

            // Create a dictionary to store vent exit event data
            var exitVentEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code },               // Game code
                { "EventType", "PlayerExitVentEvent" },    // Type of event
                { "Timestamp", DateTime.UtcNow },          // Event timestamp
                { "PlayerId", e.PlayerControl.PlayerId },  // Player ID
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "")},
                { "VentName", e.Vent.Name },               // Vent name
                { "VentId", e.Vent.Id }                    // Vent ID
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, exitVentEventData);

            // Update the player state in the GameState
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    // Set player state back to "alive" after exiting the vent
                    player.State = "alive";
                    player.Movements.Add(new GameEventCacheManager.CustomMovement(e.PlayerControl.NetworkTransform.Position, DateTimeOffset.UtcNow));
                    await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                }
            }
        }

        /// <summary>
        /// Handles the event when a player vents to another location.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerVentEvent(IPlayerVentEvent e)
        {
            _logger.LogInformation("Player {player} vented to {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.NewVent.Name, e.NewVent.Id);

            // Create a dictionary to store venting event data
            var ventEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Unique identifier for the event
                { "GameCode", e.Game.Code },               // Game code
                { "EventType", "PlayerVentEvent" },        // Type of event
                { "Timestamp", DateTime.UtcNow },          // Event timestamp
                { "PlayerId", e.PlayerControl.PlayerId },  // Player ID
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "")},
                { "VentName", e.NewVent.Name },            // Vent name
                { "VentId", e.NewVent.Id }                 // Vent ID
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, ventEventData);

            // Update the player's state in the GameState
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    // Update player state to "venting"
                    player.State = "venting";
                    player.Movements.Add(new GameEventCacheManager.CustomMovement(e.PlayerControl.NetworkTransform.Position, DateTimeOffset.UtcNow));
                    await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                }
            }
        }

        /// <summary>
        /// Handles the event when a player votes during a meeting.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerVoted(IPlayerVotedEvent e)
        {
            _logger.LogDebug("Player {player} voted for {type} {votedFor}", e.PlayerControl.PlayerInfo.PlayerName, e.VoteType, e.VotedFor?.PlayerInfo.PlayerName);

            // Create a dictionary to store vote event data
            var voteEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "PlayerVoted" },
                { "Timestamp", DateTime.UtcNow },
                { "VoterId", e.PlayerControl.PlayerId },
                { "PlayerVoter", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "")},
                { "PlayerVoted", e.VotedFor?.PlayerInfo.PlayerName != null ? e.VotedFor.PlayerInfo.PlayerName : "No Vote"},
                { "VoteType", e.VoteType.ToString() }
            };
            
            await _eventCacheManager.AddEventAsync(e.Game.Code, voteEventData);

            // Update the vote count of the voted player
            if (e.VotedFor != null)
            {
                var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
                if (gameState != null)
                {
                    var votedPlayer = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.VotedFor?.PlayerInfo.PlayerName != null ? e.VotedFor.PlayerInfo.PlayerName : "No Vote", StringComparison.OrdinalIgnoreCase));
                    if (votedPlayer != null)
                    {
                        votedPlayer.VoteCount++;
                        await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                    }
                }
            }
        }

        /// <summary>
        /// Handles the event when a player murders another player.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerMurder(IPlayerMurderEvent e)
        {
            _logger.LogDebug("Player {player} killed {killedCrewmate}", e.PlayerControl.PlayerInfo.PlayerName, e.Victim.PlayerInfo.PlayerName);

            // Create a dictionary to store murder event data
            var murderEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "PlayerMurder" },
                { "Timestamp", DateTime.UtcNow },
                { "KillerId", e.PlayerControl.PlayerId },
                { "KillerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "") },
                { "VictimId", e.Victim.PlayerId },
                { "VictimName", e.Victim.PlayerInfo.PlayerName.Replace(" ", "") },
                { "DeathReason", "Murder" } 
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, murderEventData);

            // Update the state of the murdered player (victim) in GameState
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                var victim = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.Victim.PlayerInfo.PlayerName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (victim != null)
                {
                    victim.State = "dead";
                    await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                }

            }
        }

        /// <summary>
        /// Handles the event when a player repairs a system.
        /// </summary>
        [EventListener]
        public void OnPlayerRepairSystem(IPlayerRepairSystemEvent e)
        {
            _logger.LogDebug("Player {player} repaired {system}", e.PlayerControl.PlayerInfo.PlayerName, e.SystemType);

            // Create a dictionary to store repair event data
            var repairEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "PlayerRepairSystem" },
                { "Timestamp", DateTime.UtcNow },
                { "PlayerId", e.PlayerControl.PlayerId },
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "") },
                { "SystemType", e.SystemType.ToString() }
            };

            // Save the event in the cach
            _eventCacheManager.AddEventAsync(e.Game.Code, repairEventData);
        }

        /// <summary>
        /// Handles the event when a player completes a task.
        /// </summary>
        [EventListener]
        public async ValueTask OnPlayerCompletedTaskEvent(IPlayerCompletedTaskEvent e)
        {
            _logger.LogInformation("Player {player} completed {task}, {type}, {category}, visual {visual}", e.PlayerControl.PlayerInfo.PlayerName, e.Task.Task.Name, e.Task.Task.Type, e.Task.Task.Category, e.Task.Task.IsVisual);

            // Create a dictionary to store task completion event data
            var taskEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "PlayerCompletedTask" },
                { "Timestamp", DateTime.UtcNow },
                { "PlayerId", e.PlayerControl.PlayerId },
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", "") },
                { "TaskName", e.Task.Task.Name },
                { "TaskType", e.Task.Task.Type.ToString() },
                { "IsVisual", e.Task.Task.IsVisual }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, taskEventData);

            // Update the player state in GameState
            var gameState = await _eventCacheManager.GetGameStateAsync(e.Game.Code);
            if (gameState != null)
            {
                var player = gameState.Players.FirstOrDefault(p => p.Name.Equals(e.PlayerControl.PlayerInfo.PlayerName.Replace(" ", ""), StringComparison.OrdinalIgnoreCase));
                if (player != null)
                {
                    player.Movements.Add(new GameEventCacheManager.CustomMovement(e.PlayerControl.NetworkTransform.Position, DateTimeOffset.UtcNow));

                    await _eventCacheManager.UpdateGameStateAsync(e.Game.Code, gameState);
                }
            }
        }
    }

}
