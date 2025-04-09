using System.Collections.Generic;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IGameSessionProvider
    {
        IEnumerable<string> GetActiveSessions();
    }
}
