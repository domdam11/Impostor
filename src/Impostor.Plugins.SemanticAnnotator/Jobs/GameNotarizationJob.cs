using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Coravel.Invocable;
using Impostor.Plugins.SemanticAnnotator.Ports;
using CppSharp.Types.Std;
using Impostor.Api.Events;
using Impostor.Api.Net;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class GameNotarizationJob : IInvocable
    {
        private readonly IArgumentationResultBuffer _resultBuffer;
        private readonly INotarizationService _notarizationService;
        private readonly ILogger<GameNotarizationJob> _logger;

        public GameNotarizationJob(
            IArgumentationResultBuffer resultBuffer,
            INotarizationService notarizationService,
            ILogger<GameNotarizationJob> logger)
        {
            _resultBuffer = resultBuffer;
            _notarizationService = notarizationService;
            _logger = logger;
        }

        public async Task Invoke()
        {
            _logger.LogInformation("[GameNotarizationJob] Esecuzione avviata.");

            // 1. Notarizza gli eventi di gioco registrati nella cache
            await _notarizationService.DispatchNotarizationTasksAsync("", "", new System.Collections.Generic.List<IEvent>(), new System.Collections.Generic.List<IClientPlayer>());

            // 2. Notarizza le annotazioni semantiche pronte
            while (_resultBuffer.TryGetNext(out var gameCode, out var result))
            {
                await _notarizationService.NotifyAsync(gameCode,"", "", result);
                _logger.LogInformation($"[GameNotarizationJob] Notarizzazione semantica completata per {gameCode}.");
                _resultBuffer.MarkAsProcessed(gameCode);
            }
        }
    }
}
