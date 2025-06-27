using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Games;
using Impostor.Api.Innersloth;
using Impostor.Api.Net;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Models;
using IO.Swagger.Model;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using TransactionHandler.Tasks;

public class NotarizationAdapter : INotarizationService
{
    private readonly ITransactionManager _transactionManager;
    private readonly GameEventCacheManager _eventCacheManager;
    private readonly ILogger<NotarizationAdapter> _logger;

    public NotarizationAdapter(ITransactionManager transactionManager, GameEventCacheManager eventCacheManager, ILogger<NotarizationAdapter> logger)
    /*inizializza l'adapter con le dipendenze necessarie*/
    {
        _transactionManager = transactionManager;
        _eventCacheManager = eventCacheManager;
        _logger = logger;
    }

    public async Task NotifyAsync(string gameSessionId, string eventId, string annotatedReasoning, string metadata)
    /*notifica un evento creando una nuova transazione tramite il metodo createeventasync del gestore di transazioni*/
    {
        await _transactionManager.CreateEventAsync(gameSessionId, eventId, annotatedReasoning, metadata);
        // Crea una nuova transazione per l'evento di annotazione
    }

    public async Task<List<IEvent>> DispatchNotarizationTasksAsync(string gameCode, string assetKey, IEnumerable<IEvent> events, IEnumerable<IClientPlayer> players)
    /*elabora una serie di eventi di gioco attivi e li gestisce in base al tipo*/
    {
        var processedEvents = new List<IEvent>();
        {
            foreach (var ev in events)
            {
                try
                {
                    switch (ev)
                    {
                        case IGamePlayerLeftEvent gamePlayerLeftEvent:

                            await _transactionManager.RemovePlayerAsync(gameCode, gamePlayerLeftEvent.Player.Client.Name, "");
                            processedEvents.Add(ev);
                            break;

                        case IGameStartedEvent gameStartedEvent:

                            await _transactionManager.CreateGameSessionAsync(assetKey, "nuova sessione");
                            
                            foreach (var player in players)
                            {
                                await _transactionManager.AddPlayerAsync(assetKey, player.Client.Name, "");
                            }
                            processedEvents.Add(ev);
                            break;

                        case IGameEndedEvent gameEndedEvent:
                            await _transactionManager.EndGameSessionAsync(assetKey);
                            processedEvents.Add(ev);
                            break;

                        case IGamePlayerJoinedEvent gamePlayerJoinedEvent:
                            await _transactionManager.AddPlayerAsync(gameCode, gamePlayerJoinedEvent.Player.Client.Name, "");
                            processedEvents.Add(ev);
                            break;

                        case IGameCreatedEvent gameCreatedEvent:
                        {
                            await _transactionManager.CreateGameSessionAsync(gameCode, gameCreatedEvent.Game.GameState.ToString());
                            processedEvents.Add(ev);
                            break;
                        }

                        case IGameDestroyedEvent gameDestroyed:
                        {
                            await _transactionManager.EndGameSessionAsync(gameCode);
                            processedEvents.Add(ev);
                            break;
                        }

                        default:
                            break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante l'elaborazione dell'evento {EventType} per il gioco {GameCode}", ev.GetType().Name, assetKey);
                }

            }
 
        }

        return processedEvents;
    }
  
}
