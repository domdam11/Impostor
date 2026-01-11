using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Coravel.Invocable;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Impostor.Plugins.SemanticAnnotator.Models;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class ArgumentationJob : IInvocable
    {
        private readonly IAnnotationBuffer _buffer;
        private readonly IArgumentationService _argumentation;
        private readonly IArgumentationResultBuffer _resultBuffer;
        private readonly ILogger<ArgumentationJob> _logger;

        public ArgumentationJob(IAnnotationBuffer buffer, IArgumentationService argumentation, IArgumentationResultBuffer resultBuffer, ILogger<ArgumentationJob> logger)
        {
            _buffer = buffer;
            _argumentation = argumentation;
            _resultBuffer = resultBuffer;
            _logger = logger;
        }

        public async Task Invoke()
        {
            while (_buffer.TryGetNext(out var gameCode, out AnnotationData annotationData))
            {
                var result = await _argumentation.SendAnnotationsAsync(annotationData.OwlDescription);
                _resultBuffer.Save(gameCode, result);
                _buffer.MarkAsProcessed(gameCode);
                _logger.LogInformation($"[ArgumentationJob] Reasoning completato per {gameCode}");
            }
        }
    }
}
