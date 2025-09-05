using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Annotator;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class DecisionSupportScheduler : IHostedService, IDisposable
    {
        private readonly ILogger<DecisionSupportScheduler> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _intervalMs;
        private CancellationTokenSource? _cts;
        private Task? _backgroundTask;

        public DecisionSupportScheduler(
            ILogger<DecisionSupportScheduler> logger,
            IServiceProvider serviceProvider)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _intervalMs = 5000; // oppure recupera da config/variabile
        }

        public Task StartAsync(CancellationToken cancellationToken)
        {
            _cts = new CancellationTokenSource();
            _backgroundTask = RunAsync(_cts.Token);
            return Task.CompletedTask;
        }

        private async Task RunAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var dss = scope.ServiceProvider.GetRequiredService<IDecisionSupportService>();
                    var cache = scope.ServiceProvider.GetRequiredService<GameEventCacheManager>();

                    var gameCodes = cache.GetActiveSessions();
                    await dss.ProcessMultipleAsync(gameCodes);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while executing DecisionSupport task");
                }
            }
        }

        public Task StopAsync(CancellationToken cancellationToken)
        {
            _cts?.Cancel();
            return Task.CompletedTask;
        }

        public void Dispose() => _cts?.Dispose();
    }

}
