using System;
using Impostor.Api.Events;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class GameEventListener : IEventListener
    {
        private readonly ILogger<GameEventListener> _logger;
        private readonly GameEventCacheManager _eventCacheManager;
        private readonly AnnotatorEngine _annotator;

        public GameEventListener(ILogger<GameEventListener> logger, GameEventCacheManager eventCacheManager, AnnotatorEngine engine)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
            _annotator = engine;
        }

 

        [EventListener]
        public void OnGameCreated(IGameCreatedEvent e)
        {
            _logger.LogInformation("Game {code} > created", e.Game.Code);
            if (e is not null) _eventCacheManager.CreateGame(e.Game);
            _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnGameStarting(IGameStartingEvent e)
        {
            _logger.LogInformation("Game {code} > starting", e.Game.Code);
        }

        [EventListener]
        public void OnGameStarted(IGameStartedEvent e)
        {
            CsvUtility.CsvGeneratorStartGame(e.Game.Code, CsvUtility.TimeStamp.ToUnixTimeMilliseconds().ToString());
            _logger.LogInformation("Game {code} > started", e.Game.Code);
            // start game -> start annotate
            if (e is not null) _eventCacheManager.StartGame(e.Game);

            foreach (var player in e.Game.Players)
            {
                var info = player.Character!.PlayerInfo;

                _logger.LogInformation("- {player} is {role}", info.PlayerName, info.RoleType);
            }
            _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnGameEnded(IGameEndedEvent e)
        {
            CsvUtility.CsvGeneratorEndGame(e.Game.Code, CsvUtility.TimeStamp.ToUnixTimeMilliseconds().ToString());
            _logger.LogInformation("Game {code} > ended because {reason}", e.Game.Code, e.GameOverReason);
            // eng game -> stop annotate
            if (e is not null) _eventCacheManager.EndGame(e.Game.Code, _annotator, DateTime.UtcNow);
            _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnGameDestroyed(IGameDestroyedEvent e)
        {
            _logger.LogInformation("Game {code} > destroyed", e.Game.Code);
            //end game -> stop game
            if (e is not null) _eventCacheManager.EndGame(e.Game.Code, _annotator, DateTime.UtcNow, true);
            _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnGameHostChanged(IGameHostChangedEvent e)
        {
            _logger.LogInformation(
                "Game {code} > changed host from {previous} to {new}",
                e.Game.Code,
                e.PreviousHost.Character?.PlayerInfo.PlayerName,
                e.NewHost != null ? e.NewHost.Character?.PlayerInfo.PlayerName : "none"
            );
        }

        [EventListener]
        public void OnGameOptionsChanged(IGameOptionsChangedEvent e)
        {
            _logger.LogInformation(
                "Game {code} > new options because of {source}",
                e.Game.Code,
                e.ChangedBy
            );
        }

        [EventListener]
        public void OnPlayerJoined(IGamePlayerJoinedEvent e)
        {
            _logger.LogInformation("Game {code} > {player} joined", e.Game.Code, e.Player.Client.Name);
            _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnPlayerLeftGame(IGamePlayerLeftEvent e)
        {
            _logger.LogInformation("Game {code} > {player} left", e.Game.Code, e.Player.Client.Name);
            _eventCacheManager.SaveEvent(e.Game.Code, e);
        }
    }
}
