using System.Security.Claims;
using System.IdentityModel.Tokens.Jwt;

namespace Strategic.WebApi.Security
{
    public static class ClaimsPrincipalExtensions
    {
        public static string? GetUserId(this ClaimsPrincipal user) =>
            user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue(JwtRegisteredClaimNames.Sub);

        public static bool IsAdmin(this ClaimsPrincipal user) =>
            user.IsInRole("admin");

        public static bool IsAuthenticated(this ClaimsPrincipal user) =>
            user?.Identity?.IsAuthenticated == true;
    }
}
