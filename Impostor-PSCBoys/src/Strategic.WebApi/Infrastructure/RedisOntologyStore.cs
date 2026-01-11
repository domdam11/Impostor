using System.Text.Json;
using StackExchange.Redis;
using Strategic.WebApi.Models;
using Strategic.WebApi.Ports;

namespace Strategic.WebApi.Infrastructure
{
    public sealed class RedisOntologyStore : IOntologyStore
    {
        private readonly IDatabase _db;
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = true
        };

        private const string VersionsIdxKey = "strategic:ontology:versions";

        public RedisOntologyStore(IConnectionMultiplexer mux) => _db = mux.GetDatabase();

        public async Task<IReadOnlyList<OntologyVersion>> GetAllAsync()
        {
            var ids = await _db.ListRangeAsync(VersionsIdxKey, 0, -1);
            if (ids.Length == 0) return Array.Empty<OntologyVersion>();

            var tasks = ids.Select(id => _db.StringGetAsync(OntologyKey(id!))).ToList();
            await Task.WhenAll(tasks);

            var result = new List<OntologyVersion>();
            foreach (var t in tasks)
            {
                var json = await t;
                if (!json.IsNullOrEmpty)
                {
                    var v = JsonSerializer.Deserialize<OntologyVersion>(json!, JsonOpts);
                    if (v != null) result.Add(v);
                }
            }
            return result.OrderByDescending(v => v.Version).ToList();
        }

        public async Task<OntologyVersion?> GetAsync(string id)
        {
            var json = await _db.StringGetAsync(OntologyKey(id));
            if (json.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<OntologyVersion>(json!, JsonOpts);
        }

        public async Task<OntologyVersion> SaveAsync(string owlContent)
        {
            var versions = await GetAllAsync();
            var nextVer = versions.Count == 0 ? 1 : versions.Max(v => v.Version) + 1;

            var v = new OntologyVersion
            {
                Id = Guid.NewGuid().ToString("N"),
                Version = nextVer,
                CreatedAt = DateTime.UtcNow.ToString("o"),
                OwlContent = owlContent
            };

            var json = JsonSerializer.Serialize(v, JsonOpts);

            var tx = _db.CreateTransaction();
            _ = tx.StringSetAsync(OntologyKey(v.Id), json);
            _ = tx.ListRightPushAsync(VersionsIdxKey, v.Id);

            var ok = await tx.ExecuteAsync();
            if (!ok) throw new InvalidOperationException("Failed to save ontology");
            return v;
        }

        public async Task<bool> DeleteAsync(string id)
        {
            var tx = _db.CreateTransaction();
            _ = tx.KeyDeleteAsync(OntologyKey(id));
            _ = tx.ListRemoveAsync(VersionsIdxKey, id);
            return await tx.ExecuteAsync();
        }

        private static string OntologyKey(string id) => $"strategic:ontology:{id}";
    }
}
