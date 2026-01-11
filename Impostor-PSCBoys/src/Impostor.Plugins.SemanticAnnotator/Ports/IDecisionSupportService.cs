using System.Collections.Generic;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IDecisionSupportService
    {
        Task ProcessAsync(string gameCode);
        Task ProcessMultipleAsync(IEnumerable<string> gameCodes);
    }
}
