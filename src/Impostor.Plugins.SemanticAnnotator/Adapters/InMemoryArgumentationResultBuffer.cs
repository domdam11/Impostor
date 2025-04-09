using Impostor.Plugins.SemanticAnnotator.Ports;
using System.Collections.Concurrent;
using System.Linq;
namespace Impostor.Plugins.SemanticAnnotator.Adapters
{
    public class InMemoryArgumentationResultBuffer : IArgumentationResultBuffer
    {
        private readonly ConcurrentDictionary<string, (string Result, bool Processed)> _buffer = new();

        public void Save(string gameCode, string result) => _buffer[gameCode] = (result, false);

        public bool TryGetNext(out string gameCode, out string result)
        {
            var entry = _buffer.FirstOrDefault(x => !x.Value.Processed);
            if (!string.IsNullOrEmpty(entry.Key))
            {
                gameCode = entry.Key;
                result = entry.Value.Result;
                return true;
            }
            gameCode = null;
            result = null;
            return false;
        }

        public void MarkAsProcessed(string gameCode)
        {
            if (_buffer.TryGetValue(gameCode, out var entry))
            {
                _buffer[gameCode] = (entry.Result, true);
            }
        }
    }
}
