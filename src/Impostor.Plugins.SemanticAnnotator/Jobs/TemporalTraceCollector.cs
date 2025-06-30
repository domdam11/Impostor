using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Impostor.Plugins.SemanticAnnotator.Jobs
{
    public enum TracePhase
    {
        Annotation,
        Argumentation,
        Notarization,
        NotarizationOnly
    }

    public static class TemporalTraceCollector
    {
        private static readonly ConcurrentBag<(string assetKey, string eventId, TracePhase phase, double durationMs, DateTime timestamp)> _logs = new();

        public static void Log(string assetKey, string eventId, TracePhase phase, double durationMs)
        {
            _logs.Add((assetKey, eventId, phase, durationMs, DateTime.UtcNow));
        }

        public static async Task ExportToCsvAsync(string path, bool clearAfterExport = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Timestamp,AssetKey,EventId,Phase,DurationMs");

            var orderedLogs = _logs
                .OrderBy(l => l.assetKey)
                .ThenBy(l => l.eventId)
                .ThenBy(l => l.phase switch
                {
                    TracePhase.Annotation => 0,
                    TracePhase.Argumentation => 1,
                    TracePhase.Notarization => 2,
                    TracePhase.NotarizationOnly => 3,
                    _ => 4
                })
                .ToList();

            foreach (var (assetKey, eventId, phase, durationMs, timestamp) in orderedLogs)
            {
                sb.AppendLine($"{timestamp:o},{assetKey},{eventId},{phase},{durationMs.ToString(CultureInfo.InvariantCulture)}");
            }

            using var writer = new StreamWriter(path, false, Encoding.UTF8);
            await writer.WriteAsync(sb.ToString());

            if (clearAfterExport)
            {
                while (!_logs.IsEmpty)
                    _logs.TryTake(out _);
            }
        }
    }
}
