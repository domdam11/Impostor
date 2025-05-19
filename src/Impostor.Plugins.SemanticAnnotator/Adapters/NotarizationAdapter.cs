using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;
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

    public async Task NotifyAsync(string gameId, string annotatedReasoning)
    /*notifica un evento creando una nuova transazione tramite il metodo createeventasync del gestore di transazioni*/
    {
        await _transactionManager.CreateEventAsync(gameId, annotatedReasoning);
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
            var events = await _eventCacheManager.GetEventsByGameCodeAsync(gameCode);

            foreach (var ev in events)
            {
                if (!ev.TryGetValue("EventType", out var typeObj) || !ev.TryGetValue("Timestamp", out var _))
                    continue;

                string eventType = typeObj.ToString();

                // Cattura delle variabili nel contesto giusto
                tasks.Add(Task.Run(async () =>
                {
                    await semaphore.WaitAsync();
                    try
                    {
                        switch (eventType)
                        {
                            case "UpdateDescription":
                                if (ev.TryGetValue("Description", out var description))
                                    await _transactionManager.UpdateDescriptionAsync(gameCode, description.ToString());
                                break;

                            case "RemovePlayer":
                                if (ev.TryGetValue("Player", out var player))
                                    await _transactionManager.RemovePlayerAsync(gameCode, player.ToString(), "Left");
                                break;

                            case "ChangeState":
                                if (ev.TryGetValue("State", out var state))
                                    await _transactionManager.ChangeStateAsync(gameCode, state.ToString());
                                break;

                            case "AddPlayer":
                                if (ev.TryGetValue("Player", out var newPlayer))
                                    await _transactionManager.AddPlayerAsync(gameCode, newPlayer.ToString(), "Joined");
                                break;

                            case "ReadEvent":
                                await _transactionManager.GetEventDetailsAsync(gameCode);
                                break;

                            case "ReadAsset":
                                await _transactionManager.GetGameDetailsAsync(gameCode);
                                break;

                            case "CreateAsset":
                                if (ev.TryGetValue("Description", out var assetDescription))
                                    await _transactionManager.CreateGameSessionAsync(gameCode, assetDescription.ToString());
                                break;

                            case "GetClientID":
                                await _transactionManager.GetClientIdAsync();
                                break;

                            case "GameStarted":
                                await _transactionManager.ChangeStateAsync(gameCode, "started");
                                break;

                            case "GameEnded":
                                await _transactionManager.EndGameSessionAsync(gameCode);
                                break;

                            default:
                                _logger.LogWarning("Tipo di evento sconosciuto: {EventType} per il gioco {GameCode}", eventType, gameCode);
                                break;
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Errore durante l'elaborazione dell'evento {EventType} per il gioco {GameCode}", eventType, gameCode);
                    }
                    finally
                    {
                        semaphore.Release();
                    }
                }));
            }
        }

        await Task.WhenAll(tasks);
    }
        // Attende il completamento di tutte le operazioni
}
