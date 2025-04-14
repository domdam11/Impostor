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
                    case "GameCreated":
                        await _transactionManager.CreateGameSessionAsync(gameCode, "Game created");
                        break;
                    case "PlayerJoined":
                        if (ev.TryGetValue("Player", out var player))
                            await _transactionManager.AddPlayerAsync(gameCode, player.ToString(), "Joined");
                        break;
                    case "GameStarted":
                        await _transactionManager.ChangeStateAsync(gameCode, "started");
                        break;
                    case "GameEnded":
                        await _transactionManager.EndGameSessionAsync(gameCode);
                        break;
                        // altri eventi da gestire se necessario
                }
            }
        }
    }
}
