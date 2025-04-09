using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IAnnotator
    {
        Task AnnotateAsync(string gameCode);
    }
}
