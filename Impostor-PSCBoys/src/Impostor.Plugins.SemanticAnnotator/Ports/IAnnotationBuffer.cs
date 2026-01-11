using Impostor.Plugins.SemanticAnnotator.Models;

namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IAnnotationBuffer
    {
        void Save(string gameCode, AnnotationData owl);
        bool TryGetNext(out string gameCode, out AnnotationData owl);
        void MarkAsProcessed(string gameCode);
    }
}
