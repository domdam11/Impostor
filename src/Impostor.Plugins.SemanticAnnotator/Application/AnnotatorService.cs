using System;
using System.Threading.Tasks;
using Impostor.Api.Games;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.Extensions.Options;

public class AnnotatorService : IAnnotator
{
    private readonly AnnotatorEngine _engine;
    private readonly GameEventCacheManager _cacheManager;
    private readonly IAnnotationBuffer? _buffer;

    public AnnotatorService(AnnotatorEngine engine, GameEventCacheManager cacheManager, IAnnotationBuffer? buffer = null)
    {
        _engine = engine;
        _cacheManager = cacheManager;
        _buffer = buffer;
    }

    public async Task<string> AnnotateAsync(string gameCode)
    {
        if (string.IsNullOrWhiteSpace(gameCode) || gameCode == "unassigned")
            return string.Empty;
        var owl = _cacheManager.CallAnnotate(gameCode, _engine);
        
     
        _buffer?.Save(gameCode, owl); // save to buffer only if it's configured

        //gameState.CallCount += 1;
        //gameState.GameStateName = newGameStateName;
        //await _cacheManager.UpdateGameStateAsync(gameCode, gameState);
        //await _cacheManager.ClearGameEventsAsync(gameCode);

        return owl;
    }

    
}
