using System.Collections.Generic;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Handlers;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Adapters
{
    public class GameSessionProviderAdapter : IGameSessionProvider
    {
        private readonly GameEventCacheManager _eventCacheManager;

        public GameSessionProviderAdapter(GameEventCacheManager eventCacheManager)
        {
            _eventCacheManager = eventCacheManager;
        }

        public IEnumerable<string> GetActiveSessions()
        {
            return _eventCacheManager.GetActiveSessions();
        }
    }
}
