using System.Text.Json;
using StackExchange.Redis;
using Strategic.WebApi.Models;
using Strategic.WebApi.Ports;

namespace Strategic.WebApi.Infrastructure
{
    public sealed class RedisCorrectionStore : ICorrectionStore
    {
        private readonly IDatabase _db;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public RedisCorrectionStore(IConnectionMultiplexer mux) => _db = mux.GetDatabase();

        private static string CorrKey(string sessionId, string eventId, string userId)
            => $"strategic:corr:{sessionId}:{eventId}:{userId}";

        public async Task<Correction?> GetCorrectionAsync(string sessionId, string eventId, string userId)
        {
            var json = await _db.StringGetAsync(CorrKey(sessionId, eventId, userId));
            if (json.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<Correction>(json!, JsonOpts);
        }

        public async Task<bool> UpsertCorrectionAsync(Correction c)
        {
            if (c is null) throw new ArgumentNullException(nameof(c));
            if (string.IsNullOrWhiteSpace(c.SessionId) ||
                string.IsNullOrWhiteSpace(c.EventId) ||
                string.IsNullOrWhiteSpace(c.UserId))
                throw new ArgumentException("Invalid correction");

            if (string.IsNullOrWhiteSpace(c.Timestamp))
                c.Timestamp = DateTime.UtcNow.ToString("o");

            return await _db.StringSetAsync(CorrKey(c.SessionId, c.EventId, c.UserId),
                                            JsonSerializer.Serialize(c, JsonOpts));
        }

        public async Task<bool> DeleteCorrectionAsync(string sessionId, string eventId, string userId)
        {
            return await _db.KeyDeleteAsync(CorrKey(sessionId, eventId, userId));
        }
    }

}
