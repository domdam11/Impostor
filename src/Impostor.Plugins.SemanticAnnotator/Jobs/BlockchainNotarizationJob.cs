using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Coravel.Invocable;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class BlockchainNotarizationJob : IInvocable
    {
        private readonly IArgumentationResultBuffer _resultBuffer;
        private readonly ILogger<BlockchainNotarizationJob> _logger;

        public BlockchainNotarizationJob(IArgumentationResultBuffer resultBuffer, ILogger<BlockchainNotarizationJob> logger)
        {
            _resultBuffer = resultBuffer;
            _logger = logger;
        }

        public Task Invoke()
        {
            while (_resultBuffer.TryGetNext(out var gameCode, out var result))
            {
                // TODO: Call Blockchain Notification Adapter here
                _logger.LogInformation($"[BlockchainNotarizationJob] Notifica su Blockchain per {gameCode}: {result}");
                _resultBuffer.MarkAsProcessed(gameCode);
            }
            return Task.CompletedTask;
        }
    }
}
