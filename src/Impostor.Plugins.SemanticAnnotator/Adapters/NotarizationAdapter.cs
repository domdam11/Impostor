using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TransactionHandler.Tasks;

public class NotarizationAdapter : INotarizationService
{
    private readonly ITransactionManager _transactionManager;
    private readonly GameEventCacheManager _eventCacheManager;
    private readonly ILogger<NotarizationAdapter> _logger;

    public NotarizationAdapter(ITransactionManager transactionManager, GameEventCacheManager eventCacheManager, ILogger<NotarizationAdapter> logger)ù
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
    var semaphore = new SemaphoreSlim(5);
    // Limita il numero di operazioni concorrenti a 5, si può modificare in base alle esigenze (assicurati di non superare i limiti del server o della rete)
    var tasks = new List<Task>();
    foreach (var gameCode in sessions)
    {
        var events = await _eventCacheManager.GetEventsByGameCodeAsync(gameCode);
        foreach (var ev in events)
        {
            if (!ev.TryGetValue("EventType", out var typeObj) || !ev.TryGetValue("Timestamp", out var _))
                // controlla se l'evento ha un tipo e un timestamp validi

                continue;

            string eventType = typeObj.ToString();
            // Ottieni il tipo di evento

            tasks.Add(tasks.Run(async () =>
            {
                await semaphore.WaitAsync(); // Attende fino a quando il semaforo non è disponibile
                try
                {
<<<<<<< HEAD
                    case "UpdateDescription":
                        await _transactionManager.UpdateDescriptionAsync(gameCode, description);
                        break;
                    case "RemovePlayer":
                        await _transactionManager.RemovePlayerAsync(gameCode, player.ToString());
                        break;
                    //come gestire la creazione di un Asset?
                    case "ChangeState":
                        await_transactionManager.ChangeStateAsync(gameCode, state);
                        break;
                    case "AddPlayer":
                        await _transactionManager.AddPlayerAsync(gameCode, player.ToString(), "Joined");
                        break;
                    case "ReadEvent":
                        await _transactionManager.GetEventDetailAsync(gameCode);
                        break;
                    case "ReadAsset":
                        await _transactionManager.GetGameDetailsAsync(gameCode);
                    case "CreateAsset":
                        await _transactionManager.CreateGameSessionAsync(gameCode, description);
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
                    // altri casi da gestire
                    default:
                        _logger.LogWarning("Tipo di evento sconosciuto: {EventType} per il gioco {GameCode}", eventType, gameCode);
                        break;
=======
                    switch (eventType)
                    {
                        case "UpdateDescription":
                            if (ev.TryGetValue("Description", out var description))
                            {
                                await _transactionManager.UpdateDescriptionAsync(gameCode, description.ToString());
                                /*aggiorno la descrizione della sessione di gioco*/
                            }
                            break;

                        case "RemovePlayer":
                            if (ev.TryGetValue("Player", out var player))
                            {
                                await _transactionManager.RemovePlayerAsync(gameCode, player.ToString(), "Left");
                                //rimuovo un giocatore dalla sessione di gioco
                            }
                            break;

                        case "ChangeState":
                            if (ev.TryGetValue("State", out var state))
                            {
                                await _transactionManager.ChangeStateAsync(gameCode, state.ToString());
                                //cambio lo stato della sessione di gioco
                            }
                            break;

                        case "AddPlayer":
                            if (ev.TryGetValue("Player", out var newPlayer))
                            {
                                await _transactionManager.AddPlayerAsync(gameCode, newPlayer.ToString(), "Joined");
                                //aggiungo un nuovo giocatore alla sessione di gioco
                            }
                            break;

                        case "ReadEvent":
                            await _transactionManager.GetEventDetailsAsync(gameCode);
                            //leggo i dettagli di un evento di gioco
                            break;

                        case "ReadAsset":
                            await _transactionManager.GetGameDetailsAsync(gameCode);
                            //leggo i dettagli di un asset di gioco
                            break;

                        case "CreateAsset":
                            if (ev.TryGetValue("Description", out var assetDescription))
                            {
                                await _transactionManager.CreateGameSessionAsync(gameCode, assetDescription.ToString());
                                //creo una nuova sessione di gioco con la descrizione fornita
                            }
                            break;

                        case "GetClientID":
                            await _transactionManager.GetClientIdAsync();
                            //ottengo l'ID del client
                            break;

                        case "GameStarted":
                            await _transactionManager.ChangeStateAsync(gameCode, "started");
                            //cambio lo stato della sessione di gioco in "iniziata"
                            break;

                        case "GameEnded":
                            await _transactionManager.EndGameSessionAsync(gameCode);
                            //termino la sessione di gioco
                            break;

                        default:
                            _logger.LogWarning("Tipo di evento sconosciuto: {EventType} per il gioco {GameCode}", eventType, gameCode);
                            //gestisco un tipo di evento sconosciuto
                            break;
                    }
                    }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Errore durante l'elaborazione dell'evento {EventType} per il gioco {GameCode}", eventType, gameCode);
                    //gestisco un errore durante l'elaborazione dell'evento
                    // Potresti voler registrare l'errore o eseguire altre azioni di gestione degli errori qui.
                }
                finally
                {
                    semaphore.Release();
                    // Rilascia il semaforo per consentire ad altre operazioni di procedere
                }
            }));
        }
    }
    await Task.WhenAll(tasks);
    // Attende il completamento di tutte le operazioni
}
