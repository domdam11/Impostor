using System;
using System.Numerics;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Innersloth.Customization;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.SemanticAnnotator.Handlers
{
    public class PlayerEventListener : IEventListener
    {
        private readonly Random _random = new Random();
        private readonly ILogger<PlayerEventListener> _logger;
        private readonly GameEventCacheManager _eventCacheManager;

        public PlayerEventListener(ILogger<PlayerEventListener> logger, GameEventCacheManager eventCacheManager)
        {
            _logger = logger;
            _eventCacheManager = eventCacheManager;
        }

        [EventListener]
        public async Task OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            _logger.LogInformation("Player {player} > spawned", e.PlayerControl.PlayerInfo.PlayerName);

            // Crea un dizionario per rappresentare le informazioni sull'evento
            var spawnEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                // Codice del gioco
                { "EventType", "PlayerSpawned" },           // Tipo di evento
                { "Timestamp", DateTime.UtcNow },           // Timestamp dell'evento
                { "PlayerSpawned", e.PlayerControl.PlayerInfo.PlayerName }  
            };

            // Salva l'evento nella cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, spawnEventData);

            Task.Run(async () =>
            {
                _logger.LogDebug("Starting player task");

                await Task.Delay(TimeSpan.FromSeconds(3));

                while (e.ClientPlayer.Client.Connection != null && e.ClientPlayer.Client.Connection.IsConnected)
                {
                    // Modifica le proprietà del giocatore
                    await e.PlayerControl.SetColorAsync((ColorType)_random.Next(1, 9));

                    await Task.Delay(TimeSpan.FromMilliseconds(5000));
                }

                _logger.LogDebug("Stopping player task");
            });
        }

        [EventListener]
        public void OnPlayerMovement(IPlayerMovementEvent e)
        {
            // Log dei movimenti del giocatore e aggiunta alla cache
            var movementEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                // Codice del gioco
                { "EventType", "PlayerMovement" },          // Tipo di evento
                { "Timestamp", DateTime.UtcNow },           // Timestamp dell'evento
                { "PlayerName", e.PlayerControl.PlayerInfo.PlayerName },
                { "X", e.PlayerControl.NetworkTransform.Position.X},
                { "Y", e.PlayerControl.NetworkTransform.Position.Y} 
            };

            // Aggiungi l'evento alla cache
            _eventCacheManager.AddEventAsync(e.Game.Code, movementEventData);

            // Opzionale: Generazione CSV
            CsvUtility.CsvGenerator(e.Game.Code, CsvUtility.TimeStamp.ToUnixTimeMilliseconds().ToString(),
                                     e.PlayerControl.PlayerInfo.PlayerName,
                                     e.PlayerControl.NetworkTransform.Position.X.ToString(),
                                     e.PlayerControl.NetworkTransform.Position.Y.ToString());
        }

        [EventListener]
        public void OnPlayerDestroyed(IPlayerDestroyedEvent e)
        {
            _logger.LogInformation("Player {player} > destroyed", e.PlayerControl.PlayerInfo.PlayerName);
        }

        [EventListener]
        public async ValueTask OnPlayerChatAsync(IPlayerChatEvent e)
        {
            _logger.LogInformation("Player {player} > said {message}", e.PlayerControl.PlayerInfo.PlayerName, e.Message);

            // Risponde a comandi specifici nella chat
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

        [EventListener]
        public void OnPlayerStartMeetingEvent(IPlayerStartMeetingEvent e)
        {
            _logger.LogInformation("Player {player} > started meeting, reason: {reason}", e.PlayerControl.PlayerInfo.PlayerName, e.Body == null ? "Emergency call button" : "Found the body of the player " + e.Body.PlayerInfo.PlayerName);
        }

        [EventListener]
        public void OnPlayerEnterVentEvent(IPlayerEnterVentEvent e)
        {
            _logger.LogInformation("Player {player} entered the vent in {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.Vent.Name, e.Vent.Id);
            
            // Crea un dizionario per rappresentare le informazioni sull'evento
            var playerEnterVentEventData = new Dictionary<string, object>
            {
                { "EventId", Guid.NewGuid().ToString() },  // Usa un GUID come EventId
                { "GameCode", e.Game.Code },                // Codice del gioco
                { "EventType", "PlayerEnterVentEvent" },           // Tipo di evento
                { "Timestamp", DateTime.UtcNow },           // Timestamp dell'evento
                { "Player", e.PlayerControl.PlayerInfo.PlayerName },
                { "VentName", e.Vent.Name },
                { "VentId", e.Vent.Id }
            };

            // Salva l'evento nella cache
            await _eventCacheManager.AddEventAsync(e.Game.Code, playerEnterVentEventData);
        }

        [EventListener]
        public void OnPlayerExitVentEvent(IPlayerExitVentEvent e)
        {
            _logger.LogInformation("Player {player} exited the vent in {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.Vent.Name, e.Vent.Id);
        }

        [EventListener]
        public void OnPlayerVentEvent(IPlayerVentEvent e)
        {
            _logger.LogInformation("Player {player} vented to {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.NewVent.Name, e.NewVent.Id);
        }

        [EventListener]
        public void OnPlayerVoted(IPlayerVotedEvent e)
        {
            _logger.LogDebug("Player {player} voted for {type} {votedFor}", e.PlayerControl.PlayerInfo.PlayerName, e.VoteType, e.VotedFor?.PlayerInfo.PlayerName);
        }

        [EventListener]
        public void OnPlayerCompletedTaskEvent(IPlayerCompletedTaskEvent e)
        {
            _logger.LogInformation("Player {player} completed {task}, {type}, {category}, visual {visual}", e.PlayerControl.PlayerInfo.PlayerName, e.Task.Task.Name, 
                e.Task.Task.Type, e.Task.Task.Category, e.Task.Task.IsVisual);
        }
    }
}
