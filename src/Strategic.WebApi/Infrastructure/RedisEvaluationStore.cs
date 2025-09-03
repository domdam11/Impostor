using System.Text.Json;
using StackExchange.Redis;
using Strategic.WebApi.Models;
using Strategic.WebApi.Ports;

namespace Strategic.WebApi.Infrastructure
{
    // =========================================================
    // IEvaluationStore  (indici + transazioni)
    // =========================================================
    public sealed class RedisEvaluationStore : IEvaluationStore
    {
        private readonly IDatabase _db;

        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        public RedisEvaluationStore(IConnectionMultiplexer mux) => _db = mux.GetDatabase();

        private static string EvalKey(string sessionId, string eventId, string userId)
            => $"strategic:eval:{sessionId}:{eventId}:{userId}";

        private static string EventIdxKey(string sessionId, string eventId)
            => $"strategic:evalidx:event:{sessionId}:{eventId}"; // SET di userId

        private static string UserIdxKey(string sessionId, string userId)
            => $"strategic:evalidx:user:{sessionId}:{userId}";   // SET di eventId

        public async Task<Evaluation?> GetEvaluationAsync(string sessionId, string eventId, string userId)
        {
            var json = await _db.StringGetAsync(EvalKey(sessionId, eventId, userId));
            if (json.IsNullOrEmpty) return null;
            return JsonSerializer.Deserialize<Evaluation>(json!, JsonOpts);
        }

        public async Task<IReadOnlyList<Evaluation>> GetEvaluationsByUserAsync(string sessionId, string userId)
        {
            var idxKey = UserIdxKey(sessionId, userId);
            var eventIds = await _db.SetMembersAsync(idxKey);
            if (eventIds.Length == 0) return Array.Empty<Evaluation>();

            // pipeline
            var tasks = new List<Task<RedisValue>>(eventIds.Length);
            foreach (var eid in eventIds)
                tasks.Add(_db.StringGetAsync(EvalKey(sessionId, eid!, userId)));
            await Task.WhenAll(tasks);

            var list = new List<Evaluation>(tasks.Count);
            foreach (var t in tasks)
            {
                var json = await t;
                if (!json.IsNullOrEmpty)
                {
                    var ev = JsonSerializer.Deserialize<Evaluation>(json!, JsonOpts);
                    if (ev != null) list.Add(ev);
                }
            }
            return list;
        }

        public async Task<IReadOnlyList<Evaluation>> GetEvaluationsByEventAsync(string sessionId, string eventId)
        {
            var idxKey = EventIdxKey(sessionId, eventId);
            var userIds = await _db.SetMembersAsync(idxKey);
            if (userIds.Length == 0) return Array.Empty<Evaluation>();

            var tasks = new List<Task<RedisValue>>(userIds.Length);
            foreach (var uid in userIds)
                tasks.Add(_db.StringGetAsync(EvalKey(sessionId, eventId, uid!)));
            await Task.WhenAll(tasks);

            var list = new List<Evaluation>(tasks.Count);
            foreach (var t in tasks)
            {
                var json = await t;
                if (!json.IsNullOrEmpty)
                {
                    var ev = JsonSerializer.Deserialize<Evaluation>(json!, JsonOpts);
                    if (ev != null) list.Add(ev);
                }
            }
            return list;
        }

        public async Task<bool> CreateEvaluationAsync(Evaluation e)
        {
            ValidateEval(e);

            var key = EvalKey(e.SessionId, e.EventId, e.UserId);
            var tx = _db.CreateTransaction();
            tx.AddCondition(Condition.KeyNotExists(key));

            _ = tx.StringSetAsync(key, JsonSerializer.Serialize(Normalize(e), JsonOpts));
            _ = tx.SetAddAsync(EventIdxKey(e.SessionId, e.EventId), e.UserId);
            _ = tx.SetAddAsync(UserIdxKey(e.SessionId, e.UserId), e.EventId);

            return await tx.ExecuteAsync();
        }

        public async Task<bool> UpdateEvaluationAsync(Evaluation e)
        {
            ValidateEval(e);

            var key = EvalKey(e.SessionId, e.EventId, e.UserId);
            var tx = _db.CreateTransaction();
            tx.AddCondition(Condition.KeyExists(key));

            _ = tx.StringSetAsync(key, JsonSerializer.Serialize(Normalize(e), JsonOpts));
            // garantisci indici consistenti
            _ = tx.SetAddAsync(EventIdxKey(e.SessionId, e.EventId), e.UserId);
            _ = tx.SetAddAsync(UserIdxKey(e.SessionId, e.UserId), e.EventId);

            return await tx.ExecuteAsync();
        }

        public async Task<bool> DeleteEvaluationAsync(string sessionId, string eventId, string userId)
        {
            if (string.IsNullOrWhiteSpace(sessionId) ||
                string.IsNullOrWhiteSpace(eventId) ||
                string.IsNullOrWhiteSpace(userId))
                return false;

            var key = EvalKey(sessionId, eventId, userId);
            var tx = _db.CreateTransaction();
            tx.AddCondition(Condition.KeyExists(key));

            _ = tx.KeyDeleteAsync(key);
            _ = tx.SetRemoveAsync(EventIdxKey(sessionId, eventId), userId);
            _ = tx.SetRemoveAsync(UserIdxKey(sessionId, userId), eventId);

            return await tx.ExecuteAsync();
        }

        public async Task<(int like, int dislike)> CountByEventAsync(string sessionId, string eventId)
        {
            var idxKey = EventIdxKey(sessionId, eventId);
            var userIds = await _db.SetMembersAsync(idxKey);
            if (userIds.Length == 0) return (0, 0);

            var gets = new List<Task<RedisValue>>(userIds.Length);
            foreach (var uid in userIds)
                gets.Add(_db.StringGetAsync(EvalKey(sessionId, eventId, uid!)));
            await Task.WhenAll(gets);

            int like = 0, dislike = 0;
            foreach (var t in gets)
            {
                var json = await t;
                if (json.IsNullOrEmpty) continue;

                var ev = JsonSerializer.Deserialize<Evaluation>(json!, JsonOpts);
                if (ev is null) continue;

                var r = NormalizeReaction(ev);
                if (r == "like") like++;
                else if (r == "dislike") dislike++;
            }
            return (like, dislike);
        }

        private static void ValidateEval(Evaluation e)
        {
            if (e is null) throw new ArgumentNullException(nameof(e));
            if (string.IsNullOrWhiteSpace(e.SessionId)) throw new ArgumentException("SessionId required", nameof(e.SessionId));
            if (string.IsNullOrWhiteSpace(e.EventId)) throw new ArgumentException("EventId required", nameof(e.EventId));
            if (string.IsNullOrWhiteSpace(e.UserId)) throw new ArgumentException("UserId required", nameof(e.UserId));
        }

        private static Evaluation Normalize(Evaluation e)
        {
            // Timestamp default ISO
            if (string.IsNullOrWhiteSpace(e.Timestamp))
                e.Timestamp = DateTime.UtcNow.ToString("o");

            // Value coerente con reaction se non specificato
            // (mantiene la semantica server.js: like=+1, dislike=-1, null/other=0)
            var r = NormalizeReaction(e);
            if (r == "like") e.Value = e.Value != 0 ? e.Value : 1;
            else if (r == "dislike") e.Value = e.Value != 0 ? e.Value : -1;

            return e;
        }

        private static string? NormalizeReaction(Evaluation e)
        {
            var r = e.Reaction?.Trim().ToLowerInvariant();
            if (r == "like" || r == "dislike") return r;

            // fallback dal valore numerico
            if (e.Value > 0) return "like";
            if (e.Value < 0) return "dislike";
            return null;
        }
    }
}
