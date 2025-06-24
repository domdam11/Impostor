using System.Collections.Generic;
using System.Threading.Tasks;
using Coravel.Queuing.Interfaces;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IDecisionSupportService
    {
        Task ProcessAsync(string gameCode);
        Task ProcessMultipleAsync(IEnumerable<string> gameCodes);
    }
}
