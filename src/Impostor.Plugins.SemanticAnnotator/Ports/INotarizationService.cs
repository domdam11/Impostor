using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface INotarizationService
    {
        Task NotifyAsync(string annotatedReasoning);
    }
}
