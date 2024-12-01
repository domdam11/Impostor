using Impostor.Api.Events;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;

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

        [EventListener]
        public async Task OnGameCreated(IGameCreationEvent e)
        {
            _logger.LogInformation("Game creation requested by {client}", e.Client == null ? "a plugin" : e.Client.Name);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", "unassigned" },
                { "EventType", "GameCreationRequested" },
                { "Timestamp", DateTime.UtcNow },
                { "ClientName", e.Client?.Name ?? "a plugin" }
            };

            await _eventCacheManager.AddEventAsync("unassigned", eventData);
        }

        [EventListener]
        public async Task OnGameCreated(IGameCreatedEvent e)
        {
            _logger.LogInformation("Game {code} > created", e.Game.Code);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "GameCreated" },
                { "Timestamp", DateTime.UtcNow },
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }

        [EventListener]
        public async Task OnGameStarting(IGameStartingEvent e)
        {
            _logger.LogInformation("Game {code} > starting", e.Game.Code);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "GameStarting" },
                { "Timestamp", DateTime.UtcNow },
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }

        [EventListener]
        public async Task OnGameStarted(IGameStartedEvent e)
        {
            CsvUtility.CsvGeneratorStartGame(e.Game.Code, CsvUtility.TimeStamp.ToUnixTimeMilliseconds().ToString());
            _logger.LogInformation("Game {code} > started", e.Game.Code);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "GameStarted" },
                { "Timestamp", DateTime.UtcNow }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);

            // Log players
            foreach (var player in e.Game.Players)
            {
                var info = player.Character!.PlayerInfo;
                _logger.LogInformation("- {player} is {role}", info.PlayerName, info.RoleType);
            }
        }

        [EventListener]
        public async Task OnGameEnded(IGameEndedEvent e)
        {
            CsvUtility.CsvGeneratorEndGame(e.Game.Code, CsvUtility.TimeStamp.ToUnixTimeMilliseconds().ToString());
            _logger.LogInformation("Game {code} > ended because {reason}", e.Game.Code, e.GameOverReason);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "GameEnded" },
                { "Timestamp", DateTime.UtcNow },
                { "GameOverReason", e.GameOverReason}
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }

        [EventListener]
        public async Task OnGameDestroyed(IGameDestroyedEvent e)
        {
            _logger.LogInformation("Game {code} > destroyed", e.Game.Code);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "GameDestroyed" },
                { "Timestamp", DateTime.UtcNow }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }

        [EventListener]
        public async Task OnGameHostChanged(IGameHostChangedEvent e)
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
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "GameHostChanged" },
                { "Timestamp", DateTime.UtcNow },
                { "PreviousHost", e.PreviousHost.Character?.PlayerInfo.PlayerName ?? "none" },
                { "NewHost", e.NewHost?.Character?.PlayerInfo.PlayerName ?? "none" }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }

        [EventListener]
        public async Task OnGameOptionsChanged(IGameOptionsChangedEvent e)
        {
            _logger.LogInformation(
                "Game {code} > new options because of {source}",
                e.Game.Code,
                e.ChangedBy
            );

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "GameOptionsChanged" },
                { "Timestamp", DateTime.UtcNow },
                { "ChangedBy", e.ChangedBy }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }

        [EventListener]
        public async Task OnPlayerJoined(IGamePlayerJoinedEvent e)
        {
            _logger.LogInformation("Game {code} > {player} joined", e.Game.Code, e.Player.Client.Name);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "PlayerJoined" },
                { "Timestamp", DateTime.UtcNow },
                { "Player", e.Player.Client.Name }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }

        [EventListener]
        public async Task OnPlayerLeftGame(IGamePlayerLeftEvent e)
        {
            _logger.LogInformation("Game {code} > {player} left", e.Game.Code, e.Player.Client.Name);

            // Create a dictionary for the event data
            var eventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },
                { "GameCode", e.Game.Code },
                { "EventType", "PlayerLeftGame" },
                { "Timestamp", DateTime.UtcNow },
                { "Player", e.Player.Client.Name }
            };

            await _eventCacheManager.AddEventAsync(e.Game.Code, eventData);
        }
    }
}
