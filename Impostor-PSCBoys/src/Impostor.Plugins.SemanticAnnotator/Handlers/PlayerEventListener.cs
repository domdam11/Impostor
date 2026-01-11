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
using Impostor.Plugins.SemanticAnnotator.Models;
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
                                /*
 SPRITE INDEX ↔ EMOJI MAPPING (TextMeshPro Compatible)

  ┌────────┬────────────┬───────────────────────────────────────────────┐
  │ Index  │ Emoji      │ Description                                   │
  ├────────┼────────────┼───────────────────────────────────────────────┤
  │ 0      │ 😊         │ Friendly smile (calm)                         │
  │ 1      │ 😋         │ Hungry face with tongue out                  │
  │ 2      │ 😍         │ Heart eyes (love or perfect match)           │
  │ 3      │ 😎         │ Cool face with sunglasses                    │
  │ 4      │ 😀         │ Simple smile                                 │
  │ 5      │ 😄         │ Wide smile with eyes                         │
  │ 6      │ 😂         │ Tears of joy (classic)                       │
  │ 7      │ 😃         │ Broad smile with big eyes                    │
  │ 8      │ 😄         │ Joyful laugh without sweat                   │
  │ 9      │ 😅         │ Nervous smile (sweat)                        │
  │ 10     │ 😖         │ Frustrated/discomfort face                   │
  │ 11     │ 😜         │ Tongue out with wink                         │
  │ 12     │ ❓         │ Question mark (used for unknown strategies)  │
  │ 13     │ 🤣         │ Rolling on the floor laughing (diagonal)     │
  │ 14     │ 🙂         │ Neutral smile (gentle)                       │
  │ 15     │ ☹️          │ Sad face (non-aggressive)                    │
  └────────┴────────────┴───────────────────────────────────────────────┘

*/
                                var emojiByPurpose = new Dictionary<string, string>
                                {
                                    ["plugin"] = "<sprite=3>",   // 😎 Cool face for DSS Plugin
                                    ["score"] = "<sprite=9>",    // 😅 Nervous/confident smile for score
                                    ["UccidiESpegniLuci"] = "<sprite=10>",     // 😖 (tensione)
                                    ["KillToSovrapposition"] = "<sprite=11>",  // 😜 (confusione/stacks)
                                    ["KillToWin"] = "<sprite=2>",              // 😍 (focus obiettivo)
                                    ["CanVent"] = "<sprite=1>",                // 😋 ()
                                    ["default"] = "<sprite=12>"                // ❓ (unknown strategy)
                                };
                                var strategyResponse = JsonSerializer.Deserialize<ArgumentationResponse>(strategy);
                                //var strategyDict = JsonSerializer.Deserialize<Dictionary<string, double>>(strategy);
                                var suggestedStrategies = strategyResponse.suggestedStrategies;
                                if (suggestedStrategies != null && suggestedStrategies.Count > 0)
                                {
                                    // Seleziona la strategia col valore più alto (positiva o meno negativa)
                                    var selected = suggestedStrategies.Where(a=>a != null).OrderByDescending(kvp => kvp.score).First();

                                    var strategyKey = selected.name;
                                    var score = selected.score;

                                    // Calcola percentuale da [-1,1] a [0,100]
                                    int percentage = (int)Math.Round((score + 1) * 50);

                                    // Mappa percentuale su colore
                                    string color = percentage switch
                                    {
                                        <= 33 => "red",
                                        <= 50 => "orange",
                                        <= 75 => "yellow",
                                        _ => "green"
                                    };

                                    var emoji = emojiByPurpose.ContainsKey(strategyKey) ? emojiByPurpose[strategyKey] : emojiByPurpose["default"];
                                    string pluginTitle = $"{emojiByPurpose["plugin"]} DSS Plugin";
                                    string strategyLabel = $"{emoji} " + (strategyKey switch
                                    {
                                        "UccidiESpegniLuci" => "Kill & Lights",
                                        "KillToSovrapposition" => "Kill on Stacks",
                                        "KillToWin" => "Kill to Win",
                                        "CanVent" => "Use Vent",
                                        _ => strategyKey
                                    });

                                    string explanation = strategyKey switch
                                    {
                                        "UccidiESpegniLuci" => "1. Sabotage lights before striking\n2. Choose isolated targets\n3. Escape quickly after kill",
                                        "KillToSovrapposition" => "1. Blend into groups\n2. Kill during stack tasks\n3. Avoid cameras and rush report",
                                        "KillToWin" => "1. Check win condition\n2. Target key crewmates\n3. Prevent emergency meetings",
                                        "CanVent" => "1. Wait near vent\n2. Kill and vanish fast\n3. Use sabotage to cover",
                                        _ => "Unknown\nUnknown\nUnknown"
                                    };

                                    using var writer = playerControl.Game.StartRpc(playerControl.NetId, Api.Net.Inner.RpcCalls.SetName, clientPlayer.Client.Id);
                                    Rpc06SetName.Serialize(writer,
                                        $"<align=left>" +
                                        $"<color=yellow><size=130%>{pluginTitle}</size></color>\n" +
                                        $"<color=orange><size=130%>Strategy: {strategyLabel}</size></color>\n" +
                                        $"<color=orange><size=110%>Explanation:</size></color>\n" +
                                        $"<color=orange><size=110%>{explanation}</size></color>\n" +
                                        $"<color={color}><size=150%>{emojiByPurpose["score"]} Confidence: {percentage}%</size></color>\n\n\n\n\n\n\n\n");
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
