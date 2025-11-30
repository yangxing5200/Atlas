using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    public static class TokenCacheKeys
    {
        /// <summary>
        /// 用户 TokenVersion 缓存
        /// Scope: Tenant, Expiration: 10min, L1: Enabled
        /// </summary>
        public static readonly CacheKeyDefinition UserTokenVersion = CacheKeyDefinition
            .Create("token_version:{userId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("userId")
            .WithExpiration(TimeSpan.FromMinutes(10))
            .WithDescription("User TokenVersion for revocation check")
            .EnableL1Cache(true)
            .WithMaxRandomOffset(0)
            .AllowNull(true)
            .Build();

        /// <summary>
        /// Session 黑名单（已登出的 Session）
        /// Scope: Global, Expiration: 30min, L1: Enabled
        /// </summary>
        public static readonly CacheKeyDefinition InvalidSession = CacheKeyDefinition
            .Create("invalid_session:{sessionId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("sessionId")
            .WithExpiration(TimeSpan.FromMinutes(30))
            .WithDescription("Logged out session blacklist")
            .EnableL1Cache(true)
            .WithMaxRandomOffset(0)
            .AllowNull(false)
            .Build();
    }
}
