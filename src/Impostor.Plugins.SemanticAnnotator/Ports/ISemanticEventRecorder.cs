using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Api.Events;
using Impostor.Api.Net;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface ISemanticEventRecorder
    {
        /// <summary>
        /// Notarizes a reasoning result obtained from argumentation.
        /// </summary>
        Task StoreAnnotationAsync(string gameSessionId, string eventId, string annotatedReasoning, string metadata);

        /// <summary>
        /// Dispatches notarization tasks based on cached game events.
        /// </summary>
        Task<List<IEvent>> StoreGameEventsAsync(string gameCode, string assetKey, IEnumerable<IEvent> events, IEnumerable<IClientPlayer> players);
    }
}
