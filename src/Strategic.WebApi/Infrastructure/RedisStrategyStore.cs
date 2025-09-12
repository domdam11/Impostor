using StackExchange.Redis;
using System.Text.Json;
using Strategic.WebApi.Models;
using Strategic.WebApi.Ports;

namespace Strategic.WebApi.Infrastructure
{
    public sealed class RedisStrategyStore : IStrategyStore
    {
        private readonly IDatabase _db;
        private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };
        private const string Key = "strategic:strategies";

        public RedisStrategyStore(IConnectionMultiplexer mux) => _db = mux.GetDatabase();

        public async Task<IReadOnlyList<Strategy>> GetAllAsync()
        {
            var json = await _db.StringGetAsync(Key);
            if (json.IsNullOrEmpty) return Array.Empty<Strategy>();
            return JsonSerializer.Deserialize<List<Strategy>>(json!, JsonOpts) ?? new List<Strategy>();
        }

        public async Task SaveAllAsync(IEnumerable<Strategy> strategies)
        {
            var json = JsonSerializer.Serialize(strategies, JsonOpts);
            await _db.StringSetAsync(Key, json);
        }

        public async Task<Strategy?> GetByIdAsync(string id)
        {
            var all = await GetAllAsync();
            return all.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
        }

        public async Task UpsertAsync(Strategy strategy)
        {
            var all = (await GetAllAsync()).ToList();
            var idx = all.FindIndex(s => string.Equals(s.Id, strategy.Id, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0) all[idx] = strategy;
            else all.Add(strategy);

            await SaveAllAsync(all);
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var all = (await GetAllAsync()).ToList();
            var removed = all.RemoveAll(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));
            if (removed == 0) return false;

            await SaveAllAsync(all);
            return true;
        }
    }
}
