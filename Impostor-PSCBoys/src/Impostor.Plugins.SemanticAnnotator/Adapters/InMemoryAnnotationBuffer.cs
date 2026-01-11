using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using System.Collections.Concurrent;
using System.Linq;
namespace Impostor.Plugins.SemanticAnnotator.Adapters
{
    public class InMemoryAnnotationBuffer : IAnnotationBuffer
    {
        private readonly ConcurrentDictionary<string, (AnnotationData Owl, bool Processed)> _buffer = new();

        public void Save(string gameCode, AnnotationData owl) => _buffer[gameCode] = (owl, false);

        public bool TryGetNext(out string gameCode, out AnnotationData owl)
        {
            var entry = _buffer.FirstOrDefault(x => !x.Value.Processed);
            if (!string.IsNullOrEmpty(entry.Key))
            {
                gameCode = entry.Key;
                owl = entry.Value.Owl;
                return true;
            }
            gameCode = null;
            owl = null;
            return false;
        }

        public void MarkAsProcessed(string gameCode)
        {
            if (_buffer.TryGetValue(gameCode, out var entry))
            {
                _buffer[gameCode] = (entry.Owl, true);
            }
        }
    }
}
