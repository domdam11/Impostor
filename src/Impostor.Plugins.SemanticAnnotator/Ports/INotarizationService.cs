using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Games;

public interface INotarizationService
{
    /// <summary>
    /// Notarizes a reasoning result obtained from argumentation.
    /// </summary>
    Task NotifyAsync(string gameSessionId, string eventId, string annotatedReasoning, string metadata);

    /// <summary>
    /// Dispatches notarization tasks based on cached game events.
    /// </summary>
    Task<List<IEvent>> DispatchNotarizationTasksAsync(string gameCode);
}
