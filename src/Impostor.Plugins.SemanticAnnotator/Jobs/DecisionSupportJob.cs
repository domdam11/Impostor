using System.Threading.Tasks;
using Coravel.Invocable;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Impostor.Plugins.SemanticAnnotator.Annotator;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class DecisionSupportJob : IInvocable
    {
        private readonly IDecisionSupportService _dss;
        private readonly GameEventCacheManager _cache;

        public DecisionSupportJob(IDecisionSupportService dss, GameEventCacheManager cache)
        {
            _dss = dss;
            _cache = cache;
        }

        public async Task Invoke()
        {
            var gameCodes = _cache.GetActiveSessions();
            await _dss.ProcessMultipleAsync(gameCodes);
        }
    }
}
