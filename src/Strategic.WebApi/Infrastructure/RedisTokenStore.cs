
using StackExchange.Redis;
using Strategic.WebApi.Ports;

namespace Strategic.WebApi.Infrastructure
{
    public sealed class RedisTokenStore : ITokenStore
    {
        private readonly IDatabase _db;
        public RedisTokenStore(IConnectionMultiplexer mux) => _db = mux.GetDatabase();

        private static string KeyRT(string tokenId) => $"strategic:rt:{tokenId}";
        private static string KeyIdxUser(string userId) => $"strategic:rtidx:{userId}";

        public async Task SaveRefreshTokenAsync(string tokenId, string userId, DateTime expiresUtc, string tokenHash)
        {
            var key = KeyRT(tokenId);
            var ttl = expiresUtc - DateTime.UtcNow;
            if (ttl < TimeSpan.Zero) ttl = TimeSpan.FromSeconds(1);

            var entries = new HashEntry[]
            {
            new("uid", userId),
            new("exp", expiresUtc.ToUniversalTime().Ticks.ToString()),
            new("hash", tokenHash)
            };

            await _db.HashSetAsync(key, entries);
            await _db.KeyExpireAsync(key, ttl);
            await _db.SetAddAsync(KeyIdxUser(userId), tokenId);
            await _db.KeyExpireAsync(KeyIdxUser(userId), ttl);
        }

        public async Task<(bool ok, string userId, DateTime expiresUtc)> ValidateRefreshTokenAsync(string tokenId, string tokenHash)
        {
            var key = KeyRT(tokenId);
            if (!await _db.KeyExistsAsync(key))
                return (false, "", DateTime.MinValue);

            var vals = await _db.HashGetAsync(key, new RedisValue[] { "uid", "exp", "hash" });
            var uid = vals[0];
            var expStr = vals[1];
            var storedHash = vals[2];

            if (uid.IsNullOrEmpty || expStr.IsNullOrEmpty || storedHash.IsNullOrEmpty)
                return (false, "", DateTime.MinValue);

            if (storedHash.ToString() != tokenHash)
                return (false, "", DateTime.MinValue);

            var expTicks = long.Parse(expStr!);
            var exp = new DateTime(expTicks, DateTimeKind.Utc);
            if (DateTime.UtcNow > exp)
                return (false, "", DateTime.MinValue);

            return (true, uid!, exp);
        }

        public async Task RevokeRefreshTokenAsync(string tokenId)
        {
            var key = KeyRT(tokenId);
            if (await _db.KeyExistsAsync(key))
            {
                var uid = await _db.HashGetAsync(key, "uid");
                await _db.KeyDeleteAsync(key);
                if (!uid.IsNullOrEmpty)
                    await _db.SetRemoveAsync(KeyIdxUser(uid!), tokenId);
            }
        }

        public async Task RevokeAllRefreshTokensForUserAsync(string userId)
        {
            var idx = KeyIdxUser(userId);
            var all = await _db.SetMembersAsync(idx);
            foreach (var tokenId in all)
            {
                await _db.KeyDeleteAsync(KeyRT(tokenId!));
            }
            await _db.KeyDeleteAsync(idx);
        }
    }
}
