using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Strategic.WebApi.Ports;
using StackExchange.Redis;
using Strategic.WebApi.Models;

namespace Strategic.WebApi.Infrastructure;

public sealed class RedisAuthStore : IAuthStore
{
    private readonly IDatabase _db;

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = false
    };

    public RedisAuthStore(IConnectionMultiplexer mux)
    {
        _db = mux.GetDatabase();
    }

    private static string UserKey(string userId) => $"strategic:user:{userId}";
    private static string UsersSetKey() => "strategic:users";

    // === DTO persistito (solo hash) ===
    private sealed class UserAuthHashed
    {
        public string Id { get; set; } = default!;
        public string Role { get; set; } = "user";
        public string SessionKeyHash { get; set; } = default!;
    }

    // === API ===

    public async Task<UserAuth?> GetUserAsync(string userId)
    {
        if (string.IsNullOrWhiteSpace(userId)) return null;

        var json = await _db.StringGetAsync(UserKey(userId));
        if (json.IsNullOrEmpty) return null;

        var dto = JsonSerializer.Deserialize<UserAuthHashed>(json!, JsonOpts);
        if (dto is null || string.IsNullOrWhiteSpace(dto.SessionKeyHash)) return null;

        return new UserAuth { Id = dto.Id, Role = dto.Role, SessionKey = null };
    }

    public async Task<UserAuth> CreateUserAsync(string userId, string role = "user")
    {
        if (string.IsNullOrWhiteSpace(userId))
            throw new ArgumentException("userId is required", nameof(userId));

        var exists = await _db.KeyExistsAsync(UserKey(userId));
        if (exists) throw new InvalidOperationException("User already exists");

        var sessionKey = GenerateSessionKey();
        var hash = HashPassword(sessionKey);

        var dto = new UserAuthHashed
        {
            Id = userId,
            Role = string.IsNullOrWhiteSpace(role) ? "user" : role,
            SessionKeyHash = hash
        };

        var ok = await _db.StringSetAsync(UserKey(userId), JsonSerializer.Serialize(dto, JsonOpts), when: When.NotExists);
        if (!ok) throw new InvalidOperationException("Failed to create user (race condition)");

        await _db.SetAddAsync(UsersSetKey(), userId);

        return new UserAuth { Id = userId, Role = dto.Role, SessionKey = sessionKey };
    }

    public async Task<UserAuth?> ValidateLoginAsync(string userId, string sessionKey)
    {
        if (string.IsNullOrWhiteSpace(userId) || string.IsNullOrEmpty(sessionKey)) return null;

        var json = await _db.StringGetAsync(UserKey(userId));
        if (json.IsNullOrEmpty) return null;

        var dto = JsonSerializer.Deserialize<UserAuthHashed>(json!, JsonOpts);
        if (dto is null || string.IsNullOrWhiteSpace(dto.SessionKeyHash)) return null;

        if (VerifyPassword(sessionKey, dto.SessionKeyHash))
            return new UserAuth { Id = dto.Id, Role = dto.Role, SessionKey = null };

        return null;
    }

    public async Task<IReadOnlyList<UserAuth>> GetAllUsersAsync()
    {
        var ids = await _db.SetMembersAsync(UsersSetKey());
        var list = new List<UserAuth>();

        if (ids.Length == 0) return list;

        var tasks = new List<Task<RedisValue>>(ids.Length);
        foreach (var id in ids)
            tasks.Add(_db.StringGetAsync(UserKey(id!)));
        var results = await Task.WhenAll(tasks);

        foreach (var rv in results)
        {
            if (rv.IsNullOrEmpty) continue;
            var dto = JsonSerializer.Deserialize<UserAuthHashed>(rv!, JsonOpts);
            if (dto is null) continue;
            list.Add(new UserAuth { Id = dto.Id, Role = dto.Role, SessionKey = null });
        }

        return list;
    }

    public async Task<string?> RotateSessionKeyAsync(string userId)
    {
        var json = await _db.StringGetAsync(UserKey(userId));
        if (json.IsNullOrEmpty) return null;

        var dto = JsonSerializer.Deserialize<UserAuthHashed>(json!, JsonOpts);
        if (dto is null) return null;

        var newKey = GenerateSessionKey();
        dto.SessionKeyHash = HashPassword(newKey);

        var ok = await _db.StringSetAsync(UserKey(userId), JsonSerializer.Serialize(dto, JsonOpts), when: When.Always);
        if (!ok) throw new InvalidOperationException("Rotate failed");

        return newKey;
    }

    // === Helpers ===

    private static string GenerateSessionKey()
    {
        Span<byte> buf = stackalloc byte[10];
        RandomNumberGenerator.Fill(buf);
        var b64 = Convert.ToBase64String(buf)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
        return b64;
    }

    // Hash format: v1$<iterations>$<saltB64>$<subkeyB64>
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

    private static bool VerifyPassword(string password, string hash)
    {
        try
        {
            var parts = hash.Split('$');
            if (parts.Length != 4 || parts[0] != "v1") return false;

            var iterations = int.Parse(parts[1]);
            var salt = Convert.FromBase64String(parts[2]);
            var expected = Convert.FromBase64String(parts[3]);

            var actual = Rfc2898DeriveBytes.Pbkdf2(
                password,
                salt,
                iterations,
                HashAlgorithmName.SHA256,
                expected.Length
            );

            return CryptographicOperations.FixedTimeEquals(actual, expected);
        }
        catch
        {
            return false;
        }
    }
}
