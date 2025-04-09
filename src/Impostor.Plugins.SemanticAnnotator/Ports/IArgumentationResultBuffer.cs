namespace Impostor.Plugins.SemanticAnnotator.Ports
{
    public interface IArgumentationResultBuffer
    {
        void Save(string gameCode, string result);
        bool TryGetNext(out string gameCode, out string result);
        void MarkAsProcessed(string gameCode);
    }
}
