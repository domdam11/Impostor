using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Impostor.Plugins.SemanticAnnotator.Models;
using Microsoft.Extensions.Options;

namespace Impostor.Plugins.SemanticAnnotator.Application
{
    public class DecisionSupportService : IDecisionSupportService
    {
        private readonly IAnnotator _annotator;
        private readonly IArgumentationService _argumentation;
        private readonly INotarizationService _notarization;
        private readonly ILogger<DecisionSupportService> _logger;
        private readonly bool _notarizationEnabled;
        private readonly bool _argumentationEnabled;

        public DecisionSupportService(IAnnotator annotator,
                                      IArgumentationService argumentation,
                                      INotarizationService notarization,
                                      ILogger<DecisionSupportService> logger, IOptions<ArgumentationServiceOptions> argumentationOptions, IOptions<NotarizationServiceOptions> notarizationOptions)
        {
            _annotator = annotator;
            _argumentation = argumentation;
            _notarization = notarization;
            _logger = logger;
            _notarizationEnabled = notarizationOptions.Value.Enabled;
            _argumentationEnabled = argumentationOptions.Value.Enabled;
        }

        public async Task ProcessAsync(string gameCode)
        {
            _logger.LogInformation($"[DSS] Avvio processo decisionale per {gameCode}...");

            string owl = await _annotator.AnnotateAsync(gameCode);

            if (!string.IsNullOrWhiteSpace(owl))
            {
                if (_argumentationEnabled)
                {
                    var reasoning = await _argumentation.SendAnnotationsAsync(owl);
                    if (_notarizationEnabled)
                    {
                        await _notarization.NotifyAsync(gameCode, reasoning);
                    }
                }
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
