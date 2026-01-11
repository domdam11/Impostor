using System;
using System.Threading;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public class DecisionSupportScheduler : IDisposable
    {
        private readonly ILogger<DecisionSupportScheduler> _logger;
        private readonly IServiceProvider _serviceProvider;
        private readonly int _intervalMs;
        private CancellationTokenSource? _cts;
        private Task? _backgroundTask;

        public DecisionSupportScheduler(ILogger<DecisionSupportScheduler> logger, IServiceProvider serviceProvider, IOptions<SemanticPluginOptions> options)
        {
            _logger = logger;
            _serviceProvider = serviceProvider;
            _intervalMs = options.Value.AnnotationIntervalMs;
        }

        public void Start()
        {
            if (_backgroundTask != null)
                return; // già avviato

            _cts = new CancellationTokenSource();
            _backgroundTask = RunAsync(_cts.Token);
            _logger.LogInformation("DecisionSupportScheduler started");
        }

        public void Stop()
        {
            if (_cts == null)
                return;

            _cts.Cancel();
            _backgroundTask = null;
            _logger.LogInformation("DecisionSupportScheduler stopped");
        }

        private async Task RunAsync(CancellationToken ct)
        {
            using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(_intervalMs));
            while (await timer.WaitForNextTickAsync(ct))
            {
                try
                {
                    using var scope = _serviceProvider.CreateScope();
                    var job = scope.ServiceProvider.GetRequiredService<DecisionSupportJob>();
                    await job.Invoke();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error while executing DecisionSupportJob");
                }
            }
        }

        public void Dispose() => _cts?.Dispose();
    }
}
