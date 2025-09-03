using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Models;
using Impostor.Plugins.SemanticAnnotator.Ports;
using StackExchange.Redis;
using static System.Formats.Asn1.AsnWriter;

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
                Metadata = argumentation,
                Timestamp = DateTime.UtcNow.ToString("o")
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
                var bestStrategy = details?.Metadata?.suggestedStrategies?.Where(a => a != null)
                    .OrderByDescending(s => s.score)
                    .FirstOrDefault();
                if (bestStrategy.name != "OtherStrategy" && bestStrategy?.name != null)
                {
                    result.Add(new StrategicEventSummary
                    {
                        Id = eventId,
                        Timestamp = details.Timestamp,
                        Strategy = bestStrategy?.name ?? "Unknown",
                        Score = (int)Math.Round((bestStrategy.score + 1) * 50)
                    });
                }
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
