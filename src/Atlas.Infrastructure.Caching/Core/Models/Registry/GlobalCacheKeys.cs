using System;

namespace Atlas.Infrastructure.Caching.Core.Models.Registry
{
    /// <summary>
    /// Global cache key definitions for system-wide caching.
    /// These keys are not tenant-specific and are shared across the entire system.
    /// </summary>
    public static class GlobalCacheKeys
    {
        /// <summary>
        /// Category name for registration.
        /// </summary>
        public const string Category = "Global";

        /// <summary>
        /// System configuration cache key.
        /// </summary>
        public static readonly CacheKeyDefinition SystemConfig = CacheKeyDefinition
            .Create("system:config:{key}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("key")
            .WithExpiration(TimeSpan.FromHours(1))
            .WithDescription("System configuration cache")
            .Build();

        /// <summary>
        /// Feature flags cache key.
        /// </summary>
        public static readonly CacheKeyDefinition FeatureFlags = CacheKeyDefinition
            .Create("system:feature-flags")
            .WithScope(CacheScope.Global)
            .WithExpiration(TimeSpan.FromMinutes(5))
            .WithDescription("Feature flags configuration")
            .Build();

        /// <summary>
        /// Database server configuration cache.
        /// </summary>
        public static readonly CacheKeyDefinition DatabaseServerConfig = CacheKeyDefinition
            .Create("db-server-config:{key}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("key")
            .WithExpiration(TimeSpan.FromHours(2))
            .WithDescription("Database server configuration")
            .Build();

        /// <summary>
        /// Registers all global cache keys with the registry.
        /// Should be called at application startup.
        /// </summary>
        public static void RegisterAll()
        {
            CacheKeyRegistry.Register("Global.SystemConfig", SystemConfig, Category);
            CacheKeyRegistry.Register("Global.FeatureFlags", FeatureFlags, Category);
            CacheKeyRegistry.Register("Global.DatabaseServerConfig", DatabaseServerConfig, Category);
        }
    }
}
