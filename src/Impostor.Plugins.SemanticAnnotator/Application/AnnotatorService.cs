using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Ports;
using System.Threading.Tasks;
using System;

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

        var gameState = await _cacheManager.GetGameStateAsync(gameCode);
        if (gameState == null)
            return string.Empty;

        var (results, newGameStateName) = _engine.Annotate(
            gameCode,
            _cacheManager,
            gameState.CallCount + 1,
            gameState.NumRestarts,
            DateTimeOffset.UtcNow
        );

        string owl = _engine.GetLastOwl();
        _buffer?.Save(gameCode, owl); // save to buffer only if it's configured

        gameState.CallCount += 1;
        gameState.GameStateName = newGameStateName;
        await _cacheManager.UpdateGameStateAsync(gameCode, gameState);
        await _cacheManager.ClearGameEventsAsync(gameCode);

        return owl;
    }
}
