using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using StackExchange.Redis;

namespace Impostor.Plugins.SemanticAnnotator.Infrastructure
{
    public class RedisGameEventStorage : IGameEventStorage
    {
        private readonly IDatabase _db;

        public RedisGameEventStorage(IConnectionMultiplexer connectionMultiplexer)
        {
            _db = connectionMultiplexer?.GetDatabase();
        }


        public async Task CreateGameSessionAsync(string sessionId, string description)
        {
            var session = new GameSessionInfo
            {
                Id = sessionId,
                Timestamp = DateTime.UtcNow.ToString("o")
            };

            await _db.HashSetAsync("strategic:sessions", sessionId, JsonSerializer.Serialize(session));
        }

        public async Task AddPlayerAsync(string sessionId, string playerName, string metadata)
        {
            await _db.HashSetAsync($"strategic:players:{sessionId}", playerName, metadata ?? "");
        }

        public async Task CreateEventAsync(string sessionId, string eventId, string annotatedReasoning, string metadata)
        {
            // Salva l’ID nella lista ordinata della sessione
            await _db.ListRightPushAsync($"strategic:eventlist:{sessionId}", eventId);

            // Deserializza i dettagli ricevuti
            var argumentation = JsonSerializer.Deserialize<ArgumentationResponse>(metadata);

            // Popola l'oggetto completo
            var details = new StrategicEventDetails
            {
                Id = eventId,
                Annotation = annotatedReasoning,
                Metadata = argumentation
            };

            // Salva i dettagli in Redis
            var key = $"strategic:eventdetails:{sessionId}:{eventId}";
            var json = JsonSerializer.Serialize(details);
            await _db.StringSetAsync(key, json);
        }

        public async Task<List<StrategicEventSummary>> GetEventListAsync(string sessionId)
        {
            var eventIds = await _db.ListRangeAsync($"strategic:eventlist:{sessionId}");
            var result = new List<StrategicEventSummary>();

            foreach (var redisEventId in eventIds)
            {
                var eventId = redisEventId.ToString();
                var key = $"strategic:eventdetails:{sessionId}:{eventId}";
                var json = await _db.StringGetAsync(key);
                if (!json.HasValue) continue;

                var details = JsonSerializer.Deserialize<StrategicEventDetails>(json);
                var bestStrategy = details?.Metadata?.suggestedStrategies?
                    .OrderByDescending(s => s.score)
                    .FirstOrDefault();

                result.Add(new StrategicEventSummary
                {
                    Id = int.TryParse(eventId, out var num) ? num : 0,
                    Timestamp = details?.Metadata?.graph?.nodes?.FirstOrDefault()?.data?.label ?? "",
                    Strategy = bestStrategy?.name ?? "Unknown",
                    Score = bestStrategy?.score ?? 0
                });
            }

            return result;
        }

        public async Task<StrategicEventDetails> GetEventDetailsAsync(string sessionId, string eventId)
        {
            var key = $"strategic:eventdetails:{sessionId}:{eventId}";
            var json = await _db.StringGetAsync(key);
            if (json.IsNullOrEmpty) return null;

            return JsonSerializer.Deserialize<StrategicEventDetails>(json);
        }

        public async Task<List<GameSessionInfo>> GetAllSessionsAsync()
        {
            var entries = await _db.HashGetAllAsync("strategic:sessions");
            var result = new List<GameSessionInfo>();

            foreach (var entry in entries)
            {
                if (entry.Value.IsNullOrEmpty) continue;

                var session = JsonSerializer.Deserialize<GameSessionInfo>(entry.Value);
                if (session != null)
                    result.Add(session);
            }

            return result;
        }
    }
}
