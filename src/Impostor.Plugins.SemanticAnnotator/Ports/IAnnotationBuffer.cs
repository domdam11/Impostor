namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IAnnotationBuffer
    {
        void Save(string gameCode, string owl);
        bool TryGetNext(out string gameCode, out string owl);
        void MarkAsProcessed(string gameCode);
    }
}
