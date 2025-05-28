using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.Extensions.Logging;
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
        private readonly GameEventCacheManager _cacheManager;

        public DecisionSupportService(IAnnotator annotator,
                                      IArgumentationService argumentation,
                                      INotarizationService notarization,
                                      ILogger<DecisionSupportService> logger, IOptions<ArgumentationServiceOptions> argumentationOptions, IOptions<NotarizationServiceOptions> notarizationOptions, GameEventCacheManager cacheManager)
        {
            _annotator = annotator;
            _argumentation = argumentation;
            _notarization = notarization;
            _logger = logger;
            _notarizationEnabled = notarizationOptions.Value.Enabled;
            _argumentationEnabled = argumentationOptions.Value.Enabled;
            _cacheManager = cacheManager;
        }

        public async Task ProcessAsync(string gameCode)
        {
            _logger.LogInformation($"[DSS] Avvio processo decisionale per {gameCode}...");
            var gameState = await _cacheManager.GetGameStateAsync(gameCode);
            if (gameState.IsInMatch)
            {
                string owl = await _annotator.AnnotateAsync(gameCode);


                if (!string.IsNullOrWhiteSpace(owl))
                {
                    if (_argumentationEnabled)
                    {
                        var reasoning = await _argumentation.SendAnnotationsAsync(owl);
                        if (_notarizationEnabled)
                        {
                            await _notarization.NotifyAsync(gameCode, owl);
                        }
                    }
                }
                else
                {
                    _logger.LogWarning($"[DSS] Annotazione non generata per {gameCode}");
                }


                _logger.LogInformation($"[DSS] Processo decisionale completato per {gameCode}");
            }
            else
            {
                if(_notarizationEnabled )
                {
                    await _notarization.DispatchNotarizationTasksAsync();
                }
            }
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
