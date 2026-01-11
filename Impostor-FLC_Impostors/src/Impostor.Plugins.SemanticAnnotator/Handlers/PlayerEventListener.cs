using System;
using System.Numerics;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Innersloth.Customization;
using Impostor.Api.Innersloth.GameOptions;
using Microsoft.Extensions.Logging;
using Impostor.Api.Innersloth.Maps;
using Impostor.Api.Innersloth;
using Location;
using System.IO;
using System.Reflection;
using System.Linq;

namespace Impostor.Plugins.Example.Handlers 
{
    public class PlayerEventListener : IEventListener
    {
        private readonly ILogger<PlayerEventListener> _logger;
        private readonly LocationFinding? locationFinder;

        public PlayerEventListener(ILogger<PlayerEventListener> logger)
        {
            _logger = logger;
            string? pluginDirectory = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            if (string.IsNullOrEmpty(pluginDirectory))
            {
                _logger.LogError("Impossibile determinare la directory del plugin. LocationFinding non sarà inizializzato.");
                this.locationFinder = null;
                return;
            }

            
            string mapFilePath = Path.Combine(pluginDirectory, "Data", "skeld.json");

            if (!File.Exists(mapFilePath))
            {
                _logger.LogError($"Il file della mappa non è stato trovato: {mapFilePath}. LocationFinding non sarà inizializzato.");
                this.locationFinder = null;
                return;
            }

            try
            {
                this.locationFinder = new LocationFinding(mapFilePath);
                _logger.LogInformation("LocationFinding inizializzato con successo.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Errore durante l'inizializzazione di LocationFinding: {ex.Message}");
                this.locationFinder = null;
            }
        }

        private void LogSemanticLocation(IPlayerEvent playerEvent, string eventContext)
        {
            if (this.locationFinder == null)
            {
                _logger.LogWarning($"LocationFinder non inizializzato per {eventContext}.");
                return;
            }

            var playerControl = playerEvent.PlayerControl;
            if (playerControl == null)
            {
                _logger.LogWarning($"PlayerControl è null per {eventContext}.");
                return;
            }

            string playerName = playerControl.PlayerInfo?.PlayerName ?? $"ID:{playerControl.PlayerId}";
            if (playerControl.NetworkTransform == null)
            {
                _logger.LogWarning($"PlayerControl.NetworkTransform è null per {eventContext} (Player: {playerName}).");
                return;
            }

            Vector2 playerPosition = playerControl.NetworkTransform.Position;
            try
            {
                string playerIdForLocation = playerControl.PlayerInfo?.PlayerName ?? $"Player_{playerControl.PlayerId}";
                LocationResult result = this.locationFinder.FindLocation(playerIdForLocation, playerPosition);

                
                _logger.LogInformation(
                    "------------------------------------- ({EventContext})\n" +
                    "Position: ({PlayerPositionX:F2}, {PlayerPositionY:F2})\n" +
                    "Room: {RoomName}\n" +
                    "Relative Position: ({RelativePositionX:F2}, {RelativePositionY:F2})\n" +
                    "Confidence Score: {ConfidenceScore:F2}\n" +
                    "Semantic Label: {SemanticLabel}\n" +
                    "-------------------------------------",
                    eventContext,
                    playerPosition.X, playerPosition.Y,
                    result.RoomName,
                    result.RelativePosition.X, result.RelativePosition.Y,
                    result.ConfidenceScore,
                    result.SemanticLabel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, $"Errore in locationFinder.FindLocation per {eventContext}: {ex.Message}");
            }
        }

        [EventListener]
        public void OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null) return;
            _logger.LogInformation("Player {player} > spawned", e.PlayerControl.PlayerInfo.PlayerName);
        }

        [EventListener]
        public void OnPlayerMovement(IPlayerMovementEvent e)
        {
            
            if (e.PlayerControl?.PlayerInfo == null || e.PlayerControl.NetworkTransform == null) return;

            
        }

        [EventListener]
        public void OnPlayerDestroyed(IPlayerDestroyedEvent e)
        {
            _logger.LogInformation("Player {player} > destroyed", e.PlayerControl?.PlayerInfo?.PlayerName ?? "Unknown");
        }

        [EventListener]
        public async ValueTask OnPlayerChatAsync(IPlayerChatEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null) return;
            _logger.LogInformation("Player {player} > said {message}", e.PlayerControl.PlayerInfo.PlayerName, e.Message);

            if (e.Message == "test" && e.Game?.Options != null)
            {
                e.Game.Options.NumImpostors = 2;
                if (e.Game.Options is NormalGameOptions ngo)
                {
                    ngo.KillCooldown = 0;
                    ngo.PlayerSpeedMod = 5;
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
            if (e.Message == "snap" && e.PlayerControl.NetworkTransform != null)
            {
                await e.PlayerControl.NetworkTransform.SnapToAsync(new Vector2(1, 1));
            }
            if (e.Message == "completetasks" && e.PlayerControl.PlayerInfo.Tasks != null)
            {
                foreach (var task in e.PlayerControl.PlayerInfo.Tasks)
                {
                    if (task != null) await task.CompleteAsync();
                }
            }
        }

        [EventListener]
        public void OnPlayerStartMeetingEvent(IPlayerStartMeetingEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null) return;
            string meetingReason = e.Body?.PlayerInfo == null ? "Emergency call button" : $"Found body of {e.Body.PlayerInfo.PlayerName}";
            _logger.LogInformation("Player {player} > started meeting, reason: {reason}", e.PlayerControl.PlayerInfo.PlayerName, meetingReason);
        }

        [EventListener]
        public void OnPlayerEnterVentEvent(IPlayerEnterVentEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null || e.Vent == null) return;
            _logger.LogInformation("Player {player} entered vent {vent} ({id})", e.PlayerControl.PlayerInfo.PlayerName, e.Vent.Name, e.Vent.Id);
        }

        [EventListener]
        public void OnPlayerExitVentEvent(IPlayerExitVentEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null || e.Vent == null) return;
            _logger.LogInformation("Player {player} exited vent {vent} ({id})", e.PlayerControl.PlayerInfo.PlayerName, e.Vent.Name, e.Vent.Id);
        }

        [EventListener]
        public void OnPlayerVentEvent(IPlayerVentEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null || e.NewVent == null) return;
            _logger.LogInformation("Player {player} vented to {vent} ({id})", e.PlayerControl.PlayerInfo.PlayerName, e.NewVent.Name, e.NewVent.Id);
        }

        [EventListener]
        public void OnPlayerVoted(IPlayerVotedEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null) return;
            var votedForName = e.VotedFor?.PlayerInfo?.PlayerName ?? (e.VoteType == VoteType.Skipped ? "Skip" : "Nobody/Unknown");
            _logger.LogDebug("Player {player} voted {type} for {votedFor}", e.PlayerControl.PlayerInfo.PlayerName, e.VoteType, votedForName);
        }

        [EventListener]
        public void OnPlayerCompletedTaskEvent(IPlayerCompletedTaskEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null || e.Task?.Task == null) return;
            var taskInfo = e.Task.Task;
            _logger.LogInformation("Player {player} completed {task}, type {type}, category {cat}, visual {vis}",
                e.PlayerControl.PlayerInfo.PlayerName, taskInfo.Name, taskInfo.Type, taskInfo.Category, taskInfo.IsVisual);
        }

        [EventListener]
        public void OnPlayerMurderEvent(IPlayerMurderEvent e)
        {
            if (e.PlayerControl?.PlayerInfo == null || e.Victim?.PlayerInfo == null) return;

            string killerName = e.PlayerControl.PlayerInfo.PlayerName;
            string victimName = e.Victim.PlayerInfo.PlayerName;

            _logger.LogInformation("Player {killer} killed {victim}.", killerName, victimName);

            
            if (e.PlayerControl.Game.GameState == GameStates.Started) 
            {
                LogSemanticLocation(e, $"MurderBy_{killerName}");
            }
        }
    }
}
