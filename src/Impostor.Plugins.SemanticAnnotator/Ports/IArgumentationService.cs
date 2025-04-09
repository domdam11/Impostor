using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IArgumentationService
    {
        Task<string> SendAnnotationsAsync(string owlAnnotations);
    }
}
