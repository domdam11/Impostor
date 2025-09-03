using System;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Impostor.Plugins.SemanticAnnotator.Ports;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Strategic.WebApi.Authorization;
using Strategic.WebApi.Models;
using Strategic.WebApi.Ports;

namespace Strategic.WebApi.Controllers
{
    [ApiController]
    [Route("api")]
    [Produces("application/json")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthStore _authStore;
        private readonly ITokenStore _tokenStore;
        private readonly JwtIssuer _jwt;
        private readonly JwtOptions _opt;

        public AuthController(IAuthStore authStore, ITokenStore tokenStore, JwtIssuer jwt, IOptions<JwtOptions> opt)
        {
            _authStore = authStore;
            _tokenStore = tokenStore;
            _jwt = jwt;
            _opt = opt.Value;
        }

        // ===== DTO =====
        public sealed class LoginRequest
        {
            [JsonPropertyName("userId")] public string UserId { get; set; } = default!;
            [JsonPropertyName("sessionKey")] public string SessionKey { get; set; } = default!;
        }
        public sealed class CreateUserRequest
        {
            [JsonPropertyName("userId")] public string UserId { get; set; } = default!;
            [JsonPropertyName("role")] public string? Role { get; set; } = "user";
        }

        // ===== Helpers =====
        private void SetRefreshCookie(string refreshToken, DateTime expiresUtc)
        {
            Response.Cookies.Append(_opt.RefreshCookieName, refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict, // se FE e BE sono domini diversi, valuta Lax/None
                Expires = new DateTimeOffset(expiresUtc)
            });
        }
        private void ClearRefreshCookie()
        {
            Response.Cookies.Append(_opt.RefreshCookieName, "", new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.Strict,
                Expires = DateTimeOffset.UnixEpoch
            });
        }

        // ===== Public: login / refresh / logout =====

        /// POST /api/auth/login  { userId, sessionKey } -> { access_token, user:{id,role} } + refresh cookie
        [AllowAnonymous]
        [HttpPost("auth/login")]
        public async Task<IActionResult> LoginAsync([FromBody] LoginRequest body)
        {
            if (string.IsNullOrWhiteSpace(body?.UserId) || string.IsNullOrWhiteSpace(body?.SessionKey))
                return BadRequest("Missing credentials");

            var user = await _authStore.ValidateLoginAsync(body.UserId.Trim(), body.SessionKey.Trim());
            if (user is null) return Unauthorized("Invalid credentials");

            // 1) Access token per Authorization: Bearer ...
            var accessToken = _jwt.IssueAccessToken(
                user.Id,
                string.IsNullOrWhiteSpace(user.Role) ? "user" : user.Role,
                out _
            );

            // 2) Refresh token (HttpOnly cookie) persistito nello store
            var (refreshToken, tokenId, secret, expiresUtc, hash) = _jwt.GenerateRefreshToken();
            await _tokenStore.SaveRefreshTokenAsync(tokenId, user.Id, expiresUtc, hash);
            SetRefreshCookie(refreshToken, expiresUtc);

            return Ok(new
            {
                access_token = accessToken,
                user = new { id = user.Id, role = user.Role }
            });
        }

        // helper inside AuthController
        private TokenValidationParameters BuildSigValidationParams(bool ignoreLifetime = false) => new()
        {
            ValidateIssuer = true,
            ValidIssuer = _opt.Issuer,
            ValidateAudience = true,
            ValidAudience = _opt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_opt.SigningKey)),
            ValidateLifetime = !ignoreLifetime,        // <— ignore lifetime for refresh
            ClockSkew = TimeSpan.Zero
        };

        [HttpPost("auth/refresh")]
        public async Task<IActionResult> RefreshAsync()
        {
            // 1) Require refresh cookie
            if (!Request.Cookies.TryGetValue(_opt.RefreshCookieName, out var refreshCookie))
                return Unauthorized("Missing refresh cookie");

            if (!_jwt.TryParseRefreshToken(refreshCookie, out var tokenId, out var secret))
                return Unauthorized("Invalid refresh token");

            var hash = JwtIssuer.Hash(secret, _opt.SigningKey);
            var (ok, userId, expUtc) = await _tokenStore.ValidateRefreshTokenAsync(tokenId, hash);
            if (!ok || expUtc <= DateTime.UtcNow)
                return Unauthorized("Invalid refresh token");

            // 2) Require access token in Authorization header and validate signature (lifetime ignored)
            var auth = Request.Headers["Authorization"].ToString();
            if (string.IsNullOrWhiteSpace(auth) || !auth.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                return Unauthorized("Missing access token");

            var accessToken = auth.Substring("Bearer ".Length).Trim();
            ClaimsPrincipal principal;
            try
            {
                var handler = new JwtSecurityTokenHandler();
                principal = handler.ValidateToken(accessToken, BuildSigValidationParams(ignoreLifetime: true), out _);
            }
            catch
            {
                return Unauthorized("Invalid access token");
            }

            var sub = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? principal.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            if (!string.Equals(sub, userId, StringComparison.Ordinal))
                return Unauthorized("Token subject mismatch");

            // 3) Rotate refresh + issue new access
            await _tokenStore.RevokeRefreshTokenAsync(tokenId);

            var access = _jwt.IssueAccessToken(userId, (await _authStore.GetUserAsync(userId))?.Role ?? "user", out _);

            var (rt, newId, newSecret, newExp, newHash) = _jwt.GenerateRefreshToken();
            await _tokenStore.SaveRefreshTokenAsync(newId, userId, newExp, newHash);
            SetRefreshCookie(rt, newExp);

            return Ok(new { access_token = access });
        }

        /// POST /api/auth/logout
        [HttpPost("auth/logout")]
        public async Task<IActionResult> LogoutAsync([FromQuery] bool all = false)
        {
            if (Request.Cookies.TryGetValue(_opt.RefreshCookieName, out var refreshCookie)
                && _jwt.TryParseRefreshToken(refreshCookie, out var tokenId, out _))
            {
                await _tokenStore.RevokeRefreshTokenAsync(tokenId);
            }
            ClearRefreshCookie();

            if (all && User?.Identity?.IsAuthenticated == true)
            {
                var uid = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue(JwtRegisteredClaimNames.Sub);
                if (!string.IsNullOrWhiteSpace(uid))
                    await _tokenStore.RevokeAllRefreshTokensForUserAsync(uid);
            }

            return NoContent();
        }

        // ===== Admin: create/list + rotate sessionKey =====
        [Authorize(Roles = "admin")]
        [HttpPost("admin/users")]
        public async Task<IActionResult> CreateUserAsync([FromBody] CreateUserRequest body)
        {
            var userId = body?.UserId?.Trim();
            if (string.IsNullOrWhiteSpace(userId)) return BadRequest("userId required");
            var existing = await _authStore.GetUserAsync(userId);
            if (existing is not null) return Conflict("User already exists");

            var role = string.IsNullOrWhiteSpace(body?.Role) ? "user" : body!.Role!.Trim();
            var created = await _authStore.CreateUserAsync(userId, role);
            return Created($"/api/admin/users/{created.Id}",
                new { id = created.Id, role = created.Role, sessionKey = created.SessionKey });
        }

        [Authorize(Roles = "admin")]
        [HttpGet("admin/users")]
        public async Task<IActionResult> ListUsersAsync()
        {
            var list = await _authStore.GetAllUsersAsync();
            return Ok(list ?? Array.Empty<UserAuth>());
        }


        [Authorize(Roles = "admin")]
        [HttpPost("admin/users/{userId}/rotate-session-key")]
        public async Task<IActionResult> RotateSessionKeyAsync(string userId)
        {
            var newKey = await _authStore.RotateSessionKeyAsync(userId);
            if (newKey is null) return NotFound("User not found");

            // If you use refresh tokens, revoke them for safety
            if (_tokenStore is not null)
                await _tokenStore.RevokeAllRefreshTokensForUserAsync(userId);

            return Ok(new { userId, sessionKey = newKey });
        }

    }
}
