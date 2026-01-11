using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IAnnotator
    {
        Task<AnnotationData> AnnotateAsync(string gameCode);
    }
}
