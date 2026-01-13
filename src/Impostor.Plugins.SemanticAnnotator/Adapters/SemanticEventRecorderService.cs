using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Net;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.Extensions.Logging;
using TransactionHandler.Tasks;

/// <summary>
/// Application service responsible for handling semantic event persistence,
/// through both notarization (e.g., blockchain) and optional additional storage (e.g., Redis).
/// </summary>
public class SemanticEventRecorderService : ISemanticEventRecorder
{
   // private readonly ITransactionManager _notarizer; // es. Blockchain
    private readonly IGameEventStorage _storage;     // es. Redis
    private readonly ILogger<ISemanticEventRecorder> _logger;

    public SemanticEventRecorderService(
        ITransactionManager notarizer,
        IGameEventStorage storage,
        ILogger<ISemanticEventRecorder> logger)
    {
        //_notarizer = notarizer;
        _storage = storage;
        _logger = logger;
    }

    public async Task StoreAnnotationAsync(string gameSessionId, string eventId, string annotatedReasoning, string metadata)
    {
        //await _notarizer.CreateEventAsync(gameSessionId, eventId, annotatedReasoning, metadata);
        await _storage.CreateEventAsync(gameSessionId, eventId, annotatedReasoning, metadata);
    }

    public async Task<List<IEvent>> StoreGameEventsAsync(string gameCode, string assetKey, IEnumerable<IEvent> events, IEnumerable<IClientPlayer> players)
    {
        var processedEvents = new List<IEvent>();

        foreach (var ev in events)
        {
            try
            {
                switch (ev)
                {
                    case IGamePlayerLeftEvent gamePlayerLeftEvent:
                        //await _notarizer.RemovePlayerAsync(gameCode, gamePlayerLeftEvent.Player.Client.Name, "");
                        //await _storage.RemovePlayerAsync(gameCode, gamePlayerLeftEvent.Player.Client.Name, "");
                        processedEvents.Add(ev);
                        break;

                    case IGameStartedEvent gameStartedEvent:
                        //await _notarizer.CreateGameSessionAsync(assetKey, "nuova sessione");
                        await _storage.CreateGameSessionAsync(assetKey, "nuova sessione");

                        foreach (var player in players)
                        {
                            //await _notarizer.AddPlayerAsync(assetKey, player.Client.Name, "");
                            await _storage.AddPlayerAsync(assetKey, player.Client.Name, "");
                        }
                        processedEvents.Add(ev);
                        break;

                    case IGameEndedEvent gameEndedEvent:
                        //await _notarizer.EndGameSessionAsync(assetKey);
                        //await _storage.EndGameSessionAsync(assetKey);
                        processedEvents.Add(ev);
                        break;

                    case IGamePlayerJoinedEvent gamePlayerJoinedEvent:
                        //await _notarizer.AddPlayerAsync(gameCode, gamePlayerJoinedEvent.Player.Client.Name, "");
                        await _storage.AddPlayerAsync(gameCode, gamePlayerJoinedEvent.Player.Client.Name, "");
                        processedEvents.Add(ev);
                        break;

                    case IGameCreatedEvent gameCreatedEvent:
                        //await _notarizer.CreateGameSessionAsync(gameCode, gameCreatedEvent.Game.GameState.ToString());
                        await _storage.CreateGameSessionAsync(gameCode, gameCreatedEvent.Game.GameState.ToString());
                        processedEvents.Add(ev);
                        break;

                    case IGameDestroyedEvent gameDestroyed:
                        //await _notarizer.EndGameSessionAsync(gameCode);
                        //await _storage.EndGameSessionAsync(gameCode);
                        processedEvents.Add(ev);
                        break;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Errore durante l'elaborazione dell'evento {EventType} per il gioco {GameCode}", ev.GetType().Name, assetKey);
            }
        }

        return processedEvents;
    }
}
