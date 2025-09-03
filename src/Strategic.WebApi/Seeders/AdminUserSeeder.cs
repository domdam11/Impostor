using System;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Strategic.WebApi.Seeders
{
    public static class AdminUserSeeder
    {
        private static readonly JsonSerializerOptions JsonOpts = new()
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };

        // Stesse chiavi dello store
        private static string UserKey(string userId) => $"strategic:user:{userId}";
        private static string UsersSetKey() => "strategic:users";

        // DTO persistito (stesso shape del nuovo store con hash)
        private sealed class UserAuthHashed
        {
            public string Id { get; set; } = default!;
            public string Role { get; set; } = "user";
            public string SessionKeyHash { get; set; } = default!;
        }

        /// <summary>
        /// Crea l'utente admin se non esiste già, con la sessionKey specificata,
        /// salvando in Redis SOLO l'hash PBKDF2 della chiave. Operazione idempotente.
        /// </summary>
        public static async Task SeedAsync(
            WebApplication app,
            string userId = "admin",
            string sessionKey = "admin-key-123")
        {
            using var scope = app.Services.CreateScope();
            var mux = scope.ServiceProvider.GetRequiredService<IConnectionMultiplexer>();
            var db = mux.GetDatabase();

            var key = UserKey(userId);

            // Se esiste già NON sovrascrivo (idempotente)
            var existing = await db.StringGetAsync(key);
            if (!existing.IsNullOrEmpty)
                return;

            var dto = new UserAuthHashed
            {
                Id = userId,
                Role = "admin",
                SessionKeyHash = HashPassword(sessionKey)
            };

            var json = JsonSerializer.Serialize(dto, JsonOpts);

            // Scrive solo se la chiave NON esiste (evita race)
            var ok = await db.StringSetAsync(key, json, when: When.NotExists);
            if (ok)
            {
                // Mantieni il set utenti coerente con lo store
                await db.SetAddAsync(UsersSetKey(), userId);
            }
        }

        // === PBKDF2 helpers (stessi parametri dello store) ===
        // Formato hash: v1$<iterations>$<saltB64>$<subkeyB64>
        private const int Pbkdf2Iterations = 210_000;
        private const int SaltSize = 16;
        private const int SubkeySize = 32;

        private static string HashPassword(string password)
        {
            var salt = RandomNumberGenerator.GetBytes(SaltSize);
            var subkey = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                Pbkdf2Iterations,
                HashAlgorithmName.SHA256,
                SubkeySize
            );
            return $"v1${Pbkdf2Iterations}${Convert.ToBase64String(salt)}${Convert.ToBase64String(subkey)}";
        }
    }
}
