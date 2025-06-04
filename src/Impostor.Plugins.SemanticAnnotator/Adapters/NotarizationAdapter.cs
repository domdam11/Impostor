using Impostor.Api.Events;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
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

    public async Task NotifyAsync(string gameId, string eventId, string annotatedReasoning, string metadata)
    /*notifica un evento creando una nuova transazione tramite il metodo createeventasync del gestore di transazioni*/
    {
        await _transactionManager.CreateEventAsync(gameId, eventId, annotatedReasoning, metadata);
        // Crea una nuova transazione per l'evento di annotazione
    }

    public async Task DispatchNotarizationTasksAsync()
    /*elabora una serie di eventi di gioco attivi e li gestisce in base al tipo*/
    {
        var sessions = _eventCacheManager.GetActiveSessions();
        var semaphore = new SemaphoreSlim(5); // massimo 5 operazioni concorrenti
        var tasks = new List<Task>();

        foreach (var gameCode in sessions)
        {
            var events = _eventCacheManager.GetEventsByGameCodeAsync(gameCode);

            foreach (var ev in events)
            {
                var assetKey = _eventCacheManager.GetGameSessionUniqueId(gameCode);
                // Cattura delle variabili nel contesto giusto
                //tasks.Add(Task.Run(async () =>
                //{
                    //await semaphore.WaitAsync();
                    try
                    {
                        switch (ev)
                        {
                            case IGamePlayerLeftEvent gamePlayerLeftEvent:
                               
                                await _transactionManager.RemovePlayerAsync(assetKey, gamePlayerLeftEvent.Player.Client.Name, "");
                                break;

                            case IGameStartedEvent gameStartedEvent:
                               
                                await _transactionManager.ChangeStateAsync(assetKey, "in corso");
                                break;

                            case IGameEndedEvent gameEndedEvent:

                                await _transactionManager.ChangeStateAsync(assetKey, "chiusa");
                            break;

                            case IGamePlayerJoinedEvent gamePlayerJoinedEvent:
                             
                                await _transactionManager.AddPlayerAsync(assetKey, gamePlayerJoinedEvent.Player.Client.Name, "");
                                break;

                            case IGameCreatedEvent gameCreatedEvent:
                               
                            {
                                await _transactionManager.CreateGameSessionAsync(assetKey, gameCreatedEvent.Game.GameState.ToString());
                                break;
                            }

                            default:
                               // _logger.LogWarning("Tipo di evento sconosciuto: {EventType} per il gioco {GameCode}", ev.GetType().Name, gameCode);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Errore durante l'elaborazione dell'evento {EventType} per il gioco {GameCode}", ev.GetType().Name, assetKey);
                    }
                    /*finally
                    {
                        semaphore.Release();
                    }*/
                //}));
            }
        }

        await Task.WhenAll(tasks);
    }
        // Attende il completamento di tutte le operazioni
}
