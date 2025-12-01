using System;

namespace Atlas.Infrastructure.Caching.Core.Models.Registry
{
    /// <summary>
    /// Token and session related cache key definitions.
    /// These keys are used for authentication and session management.
    /// </summary>
    public static class TokenCacheKeysV2
    {
        /// <summary>
        /// Category name for registration.
        /// </summary>
        public const string Category = "Token";

        /// <summary>
        /// User TokenVersion cache for revocation checking.
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
        /// Session blacklist for logged out sessions.
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

        /// <summary>
        /// Refresh token cache.
        /// </summary>
        public static readonly CacheKeyDefinition RefreshToken = CacheKeyDefinition
            .Create("refresh_token:{tokenId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("tokenId")
            .WithExpiration(TimeSpan.FromDays(7))
            .WithDescription("Refresh token storage")
            .EnableL1Cache(false)
            .WithMaxRandomOffset(0)
            .AllowNull(false)
            .Build();

        /// <summary>
        /// Registers all token cache keys with the registry.
        /// Should be called at application startup.
        /// </summary>
        public static void RegisterAll()
        {
            CacheKeyRegistry.Register("Token.UserTokenVersion", UserTokenVersion, Category);
            CacheKeyRegistry.Register("Token.InvalidSession", InvalidSession, Category);
            CacheKeyRegistry.Register("Token.RefreshToken", RefreshToken, Category);
        }
    }
}
