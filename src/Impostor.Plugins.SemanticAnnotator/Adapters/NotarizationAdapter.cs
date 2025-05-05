using Impostor.Plugins.SemanticAnnotator.Annotator;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using TransactionHandler.Tasks;

public class NotarizationAdapter : INotarizationService
{
    private readonly ITransactionManager _transactionManager;
    private readonly GameEventCacheManager _eventCacheManager;
    private readonly ILogger<NotarizationAdapter> _logger;

    public NotarizationAdapter(ITransactionManager transactionManager, GameEventCacheManager eventCacheManager, ILogger<NotarizationAdapter> logger)
    {
        _transactionManager = transactionManager;
        _eventCacheManager = eventCacheManager;
        _logger = logger;
    }

    public async Task NotifyAsync(string gameId, string annotatedReasoning)
    {
        await _transactionManager.CreateEventAsync(gameId, annotatedReasoning);
    }

    public async Task DispatchNotarizationTasksAsync()
    {
        var sessions = _eventCacheManager.GetActiveSessions();
        foreach (var gameCode in sessions)
        {
            var events = await _eventCacheManager.GetEventsByGameCodeAsync(gameCode);
            foreach (var ev in events)
            {
                if (!ev.TryGetValue("EventType", out var typeObj) || !ev.TryGetValue("Timestamp", out var _))
                    continue;

                string eventType = typeObj.ToString();
                switch (eventType)
                {
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
                }
            }
        }
    }
}
