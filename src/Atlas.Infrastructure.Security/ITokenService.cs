using Atlas.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Security
{
    public interface ITokenService
    {
        string GenerateToken(ICurrentIdentity user, string? extra = null);
        Task<TokenInfo?> ValidateTokenAsync(string token);
        TokenInfo? ValidateToken(string token);
    }
}
