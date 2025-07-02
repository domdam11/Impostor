using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Events.Player;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Innersloth.Customization;
using Impostor.Api.Innersloth.GameOptions;
using Impostor.Api.Net.Messages.Rpcs;
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
        public void OnPlayerSpawned(IPlayerSpawnedEvent e)
        {
            _logger.LogInformation("Player {player} > spawned", e.PlayerControl.PlayerInfo.PlayerName);

            // Need to make a local copy because it might be possible that
            // the event gets changed after being handled.
            var clientPlayer = e.ClientPlayer;
            var playerControl = e.PlayerControl;

            Task.Run(async () =>
            {
                _logger.LogDebug("Starting player task");

                // Give the player time to load.
                await Task.Delay(TimeSpan.FromSeconds(3));

                while (clientPlayer.Client.Connection != null && clientPlayer.Client.Connection.IsConnected)
                {
                    if (playerControl.Game.GameState == Api.Innersloth.GameStates.Started
                        && playerControl.PlayerInfo.IsImpostor)
                    {
                        var strategy = _eventCacheManager.GetLastStrategy(playerControl.Game.Code);
                        if (!string.IsNullOrWhiteSpace(strategy))
                        {
                            try
                            {
                                var strategyDict = JsonSerializer.Deserialize<Dictionary<string, double>>(strategy);

                                if (strategyDict != null && strategyDict.Count > 0)
                                {
                                    // Seleziona la strategia col valore più alto (positiva o meno negativa)
                                    var selected = strategyDict.OrderByDescending(kvp => kvp.Value).First();

                                    var strategyKey = selected.Key;
                                    var score = selected.Value;

                                    // Calcola percentuale da [-1,1] a [0,100]
                                    int percentage = (int)Math.Round((score + 1) * 50);

                                    // Mappa percentuale su colore
                                    string color = percentage switch
                                    {
                                        <= 33 => "red",
                                        <= 66 => "yellow",
                                        _ => "green"
                                    };

                                    string pluginTitle = "🧩 SEAL-chain mode";
                                    string strategyLabel = strategyKey switch
                                    {
                                        "UccidiESpegniLuci" => "😎 Kill & Lights",
                                        "KillToSovrapposition" => "😎 Kill on Stacks",
                                        "KillToWin" => "😎 Kill to Win",
                                        "CanVent" => "😎 Vent Kill",
                                        _ => "😐 Unknown"
                                    };

                                    string explanation = strategyKey switch
                                    {
                                        "UccidiESpegniLuci" => "😅 Sabotage lights before striking\n😁 Choose isolated targets\n😉 Escape quickly after kill",
                                        "KillToSovrapposition" => "😅 Blend into groups\n😁 Kill during stack tasks\n😉 Avoid cameras and rush report",
                                        "KillToWin" => "😅 Check win condition\n😁 Target key crewmates\n😉 Prevent emergency meetings",
                                        "CanVent" => "😅 Wait near vent\n😁 Kill and vanish fast\n😉 Use sabotage to cover",
                                        _ => "😐 Unknown\n😐 Unknown\n😐 Unknown"
                                    };

                                    using var writer = playerControl.Game.StartRpc(playerControl.NetId, Api.Net.Inner.RpcCalls.SetName, clientPlayer.Client.Id);
                                    Rpc06SetName.Serialize(writer,
                                        $"<align=left>" +
                                        $"<color=yellow><size=130%>{pluginTitle}</size></color>\n" +
                                        $"Strategy: {strategyLabel}\n" +
                                        $"Explanation:\n" +
                                        $"{explanation}\n\n" +
                                        $"<color={color}><size=150%>😇 Risk Score: {percentage}%</size></color>");
                                    await playerControl.Game.FinishRpcAsync(writer);
                                }
                            }
                            catch (Exception ex)
                            {
                                _logger.LogError(ex, "Errore durante l'invio della strategia all'impostore");
                            }
                        }
                        else
                        {
                            _logger.LogDebug("Nessuna strategia disponibile per impostore nella partita {GameCode}", playerControl.Game.Code);
                        }
                    }

                    await Task.Delay(TimeSpan.FromMilliseconds(500));
                }


                _logger.LogDebug("Stopping player task");
            });
        }

        [EventListener]
        public void OnPlayerMovement(IPlayerMovementEvent e)
        {
            foreach (var player in e.Game.Players)
            {
                if (player != null)
                {
                    if (e.PlayerControl.NetworkTransform.Position.X == player.Character?.NetworkTransform.Position.X)
                    {

                    }
                }

            }

            //_logger.LogInformation("Player {player} > movement to {position}", e.PlayerControl.PlayerInfo.PlayerName, e.PlayerControl.NetworkTransform.Position);
            CsvUtility.CsvGenerator(e.Game.Code, CsvUtility.TimeStamp.ToUnixTimeMilliseconds().ToString(), e.PlayerControl.PlayerInfo.PlayerName, e.PlayerControl.NetworkTransform.Position.X.ToString(), e.PlayerControl.NetworkTransform.Position.Y.ToString());
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
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

            //await e.PlayerControl.SetNameAsync(e.PlayerControl.PlayerInfo.PlayerName);
            await e.PlayerControl.SendChatAsync(e.Message);
        }

        [EventListener]
        public void OnPlayerStartMeetingEvent(IPlayerStartMeetingEvent e)
        {
            _logger.LogInformation("Player {player} > started meeting, reason: {reason}", e.PlayerControl.PlayerInfo.PlayerName, e.Body == null ? "Emergency call button" : "Found the body of the player " + e.Body.PlayerInfo.PlayerName);
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnPlayerEnterVentEvent(IPlayerEnterVentEvent e)
        {
            _logger.LogInformation("Player {player} entered the vent in {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.Vent.Name, e.Vent.Id);
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnPlayerExitVentEvent(IPlayerExitVentEvent e)
        {
            _logger.LogInformation("Player {player} exited the vent in {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.Vent.Name, e.Vent.Id);
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnPlayerVentEvent(IPlayerVentEvent e)
        {
            _logger.LogInformation("Player {player} vented to {vent} ({ventId})", e.PlayerControl.PlayerInfo.PlayerName, e.NewVent.Name, e.NewVent.Id);
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnPlayerVoted(IPlayerVotedEvent e)
        {
            _logger.LogDebug("Player {player} voted for {type} {votedFor}", e.PlayerControl.PlayerInfo.PlayerName, e.VoteType, e.VotedFor?.PlayerInfo.PlayerName);
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnPlayerMurder(IPlayerMurderEvent e)
        {
            _logger.LogDebug("Player {player} killed {killedCrewmate}", e.PlayerControl.PlayerInfo.PlayerName, e.Victim.PlayerInfo.PlayerName);
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
        }

        [EventListener]
        public void OnPlayerRepairSystem(IPlayerRepairSystemEvent e)
        {
            _logger.LogDebug("Player {player} repaired {system}", e.PlayerControl.PlayerInfo.PlayerName, e.SystemType);
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
        }


        [EventListener]
        public void OnPlayerCompletedTaskEvent(IPlayerCompletedTaskEvent e)
        {
            _logger.LogInformation("Player {player} completed {task}, {type}, {category}, visual {visual}", e.PlayerControl.PlayerInfo.PlayerName, e.Task.Task.Name, e.Task.Task.Type, e.Task.Task.Category, e.Task.Task.IsVisual);
            // add event in order to annotate
            if (e is not null) _eventCacheManager.SaveEvent(e.Game.Code, e);
        }
    }
}
