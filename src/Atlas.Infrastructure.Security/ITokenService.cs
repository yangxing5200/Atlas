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
        string GenerateToken(ICurrentIdentity user);
        /// <summary>
        /// Generate token using existing TokenInfo (preserves TokenVersion)
        /// </summary>
        string GenerateToken(TokenInfo tokenInfo);
        Task<TokenInfo?> ValidateTokenAsync(string token);
        TokenInfo? ValidateToken(string token);
    }

    public interface ITokenCacheService
    {
        /// <summary>
        /// 获取用户 TokenVersion（优先缓存，未命中返回 null）
        /// </summary>
        int? GetUserTokenVersion(long userId);

        /// <summary>
        /// 设置用户 TokenVersion 到缓存
        /// </summary>
        void SetUserTokenVersion(long userId, int version);

        /// <summary>
        /// 清除用户 TokenVersion 缓存（使所有 token 失效）
        /// </summary>
        void InvalidateUserTokens(long userId);

        /// <summary>
        /// 检查 Session 是否有效（不在黑名单中）
        /// </summary>
        bool IsSessionValid(string sessionId);

        /// <summary>
        /// 加入 Session 黑名单（标记为已登出）
        /// </summary>
        void InvalidateSession(string sessionId);
    }
}
