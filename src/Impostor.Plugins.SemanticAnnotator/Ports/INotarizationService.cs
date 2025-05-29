using System.Threading.Tasks;

public interface INotarizationService
{
    /// <summary>
    /// Notarizes a reasoning result obtained from argumentation.
    /// </summary>
    Task NotifyAsync(string gameId, string eventId, string annotatedReasoning, string metadata);

    /// <summary>
    /// Dispatches notarization tasks based on cached game events.
    /// </summary>
    Task DispatchNotarizationTasksAsync();
}
