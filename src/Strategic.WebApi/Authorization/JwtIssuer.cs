using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace Strategic.WebApi.Authorization
{
    public sealed class JwtIssuer
    {
        private readonly JwtOptions _opt;
        private readonly SigningCredentials _creds;

        public JwtIssuer(IOptions<JwtOptions> opt)
        {
            _opt = opt.Value;
            _creds = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey)),
                SecurityAlgorithms.HmacSha256
            );
        }

        public string IssueAccessToken(string userId, string role, out string jti)
        {
            jti = Guid.NewGuid().ToString("N");
            var now = DateTime.UtcNow;
            var exp = now.AddMinutes(_opt.AccessTokenMinutes);

            var claims = new[]
            {
            new Claim(JwtRegisteredClaimNames.Sub, userId),
            new Claim(JwtRegisteredClaimNames.Jti, jti),
            new Claim(ClaimTypes.NameIdentifier, userId),
            new Claim(ClaimTypes.Role, role)
        };

            var token = new JwtSecurityToken(
                issuer: _opt.Issuer,
                audience: _opt.Audience,
                claims: claims,
                notBefore: now,
                expires: exp,
                signingCredentials: _creds);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        // Refresh token = "<id>.<secret>"
        public (string token, string id, string secret, DateTime expiresUtc, string hash) GenerateRefreshToken()
        {
            var id = Guid.NewGuid().ToString("N");
            var secret = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
            var token = $"{id}.{secret}";
            var expires = DateTime.UtcNow.AddDays(_opt.RefreshTokenDays);
            var hash = Hash(secret, _opt.SigningKey);
            return (token, id, secret, expires, hash);
        }

        public bool TryParseRefreshToken(string token, out string id, out string secret)
        {
            id = ""; secret = "";
            if (string.IsNullOrWhiteSpace(token)) return false;
            var parts = token.Split('.', 2);
            if (parts.Length != 2) return false;
            id = parts[0];
            secret = parts[1];
            return !string.IsNullOrWhiteSpace(id) && !string.IsNullOrWhiteSpace(secret);
        }

        public static string Hash(string secret, string key)
        {
            using var h = new HMACSHA256(Encoding.UTF8.GetBytes(key));
            return Convert.ToBase64String(h.ComputeHash(Encoding.UTF8.GetBytes(secret)));
        }
    }
}
