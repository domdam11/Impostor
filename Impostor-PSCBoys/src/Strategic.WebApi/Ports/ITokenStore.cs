using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Strategic.WebApi.Ports
{
    public interface ITokenStore
    {
        Task SaveRefreshTokenAsync(string tokenId, string userId, DateTime expiresUtc, string tokenHash);
        Task<(bool ok, string userId, DateTime expiresUtc)> ValidateRefreshTokenAsync(string tokenId, string tokenHash);
        Task RevokeRefreshTokenAsync(string tokenId);
        Task RevokeAllRefreshTokensForUserAsync(string userId);
    }
}
