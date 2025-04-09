using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Ports;

namespace Impostor.Plugins.SemanticAnnotator.Application
{
    public class DecisionSupportService : IDecisionSupportService
    {
        private readonly GameEventCacheManager _cache;
        private readonly AnnotatorEngine _engine;
        private readonly IArgumentationService _argumentation;
        private readonly INotarizationService _notarization;
        private readonly ILogger<DecisionSupportService> _logger;

        public DecisionSupportService(GameEventCacheManager cache,
                                       AnnotatorEngine engine,
                                       IArgumentationService argumentation,
                                       INotarizationService notarization,
                                       ILogger<DecisionSupportService> logger)
        {
            _cache = cache;
            _engine = engine;
            _argumentation = argumentation;
            _notarization = notarization;
            _logger = logger;
        }

        public async Task ProcessAsync(string gameCode)
        {
            _logger.LogInformation($"[DSS] Avvio processo decisionale per {gameCode}...");
            var (players, _) = _engine.Annotate(gameCode, _cache, 0, 0, DateTimeOffset.UtcNow);
            string owl = _engine.GetLastOwl();
            var reasoning = await _argumentation.SendAnnotationsAsync(owl);
            await _notarization.NotifyAsync(reasoning);
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
