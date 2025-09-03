using StackExchange.Redis;
using Strategic.WebApi.Ports;

namespace Strategic.WebApi.Infrastructure
{
    // =========================================================
    // IAccessControlStore   (bypass admin)
    // =========================================================
    public sealed class RedisAccessControlStore : IAccessControlStore
    {
        private readonly IDatabase _db;
        private readonly IAuthStore _auth;

        public RedisAccessControlStore(IConnectionMultiplexer mux, IAuthStore auth)
        {
            _db = mux.GetDatabase();
            _auth = auth;
        }

        private static string PermKey(string userId) => $"strategic:perm:{userId}"; // SET di sessionId

        public async Task<IReadOnlyList<string>> GetAllowedSessionsAsync(string userId)
        {
            if (string.IsNullOrWhiteSpace(userId)) return Array.Empty<string>();
            var members = await _db.SetMembersAsync(PermKey(userId));
            return members.Select(x => (string)x).ToArray();
        }

        public async Task SetAllowedSessionsAsync(string userId, IEnumerable<string> sessionIds)
        {
            if (string.IsNullOrWhiteSpace(userId))
                throw new ArgumentException("userId is required", nameof(userId));

            var key = PermKey(userId);
            var tx = _db.CreateTransaction();
            _ = tx.KeyDeleteAsync(key);

            var list = (sessionIds ?? Array.Empty<string>())
                       .Where(s => !string.IsNullOrWhiteSpace(s))
                       .Distinct(StringComparer.Ordinal)
                       .ToArray();

            if (list.Length > 0)
            {
                _ = tx.SetAddAsync(key, list.Select(s => (RedisValue)s).ToArray());
            }

            var ok = await tx.ExecuteAsync();
            if (!ok) throw new InvalidOperationException("Failed to set permissions");
        }

        public async Task<bool> CanUserVoteAsync(string sessionId, string userId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) || string.IsNullOrWhiteSpace(userId))
                return false;

            // Bypass admin: un admin è associato a TUTTE le sessioni
            var u = await _auth.GetUserAsync(userId);
            if (string.Equals(u?.Role, "admin", StringComparison.OrdinalIgnoreCase))
                return true;

            // Altrimenti controlla l'associazione esplicita
            return await _db.SetContainsAsync(PermKey(userId), sessionId);
        }
    }
}
