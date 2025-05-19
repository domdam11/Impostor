using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Application
{
    public class DecisionSupportService : IDecisionSupportService
    {
        private readonly IAnnotator _annotator;
        private readonly IArgumentationService _argumentation;
        private readonly INotarizationService _notarization;
        private readonly ILogger<DecisionSupportService> _logger;

        public DecisionSupportService(IAnnotator annotator,
                                      IArgumentationService argumentation,
                                      INotarizationService notarization,
                                      ILogger<DecisionSupportService> logger)
        {
            _annotator = annotator;
            _argumentation = argumentation;
            _notarization = notarization;
            _logger = logger;
        }

        public async Task ProcessAsync(string gameCode)
        {
            _logger.LogInformation($"[DSS] Avvio processo decisionale per {gameCode}...");

            string owl = await _annotator.AnnotateAsync(gameCode);

            if (!string.IsNullOrWhiteSpace(owl))
            {
                var reasoning = await _argumentation.SendAnnotationsAsync(owl);
                await _notarization.NotifyAsync(gameCode, reasoning);
            }
            else
            {
                _logger.LogWarning($"[DSS] Annotazione non generata per {gameCode}");
            }

            _logger.LogInformation($"[DSS] Processo decisionale completato per {gameCode}");
        }

        public async Task ProcessMultipleAsync(IEnumerable<string> gameCodes)
        {
            foreach (var gameCode in gameCodes)
            {
                await ProcessAsync(gameCode);
            }
        }
    }
}
