using System;
using System.Collections.Concurrent;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;

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
        private static readonly ConcurrentBag<(string assetKey, string eventId, TracePhase phase, double durationMs, AnnotationData, DateTime timestamp)> _logs = new();

        public static void Log(string assetKey, string eventId, TracePhase phase, double durationMs, AnnotationData annotationData)
        {
            _logs.Add((assetKey, eventId, phase, durationMs, annotationData, DateTime.UtcNow));
        }

        public static async Task ExportToCsvAsync(string path, int testId, bool clearAfterExport = true)
        {
            var sb = new StringBuilder();
            sb.AppendLine("TestId,Timestamp,AssetKey,EventId,Phase,DurationMs,NumIndividuals,NumEntities,Bytes");

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

            foreach (var (assetKey, eventId, phase, durationMs, annotationData, timestamp) in orderedLogs)
            {
                sb.AppendLine($"{testId},{timestamp:o},{assetKey},{eventId},{phase},{durationMs.ToString(CultureInfo.InvariantCulture)},{annotationData?.NumIndividuals},{annotationData?.NumEntities},{annotationData?.SizeInBytes}");
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
