using System;

namespace Atlas.Infrastructure.Caching.Core.Models.Registry
{
    /// <summary>
    /// Tenant-related cache key definitions.
    /// These keys are scoped to specific tenants for multi-tenant isolation.
    /// </summary>
    public static class TenantCacheKeysV2
    {
        /// <summary>
        /// Category name for registration.
        /// </summary>
        public const string Category = "Tenant";

        /// <summary>
        /// Tenant database connection information cache.
        /// </summary>
        public static readonly CacheKeyDefinition TenantDbConnection = CacheKeyDefinition
            .Create("tenant-db-conn:{id}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("id")
            .WithExpiration(TimeSpan.FromHours(1))
            .WithDescription("Tenant database connection information")
            .Build();

        /// <summary>
        /// Tenant configuration cache.
        /// </summary>
        public static readonly CacheKeyDefinition TenantConfig = CacheKeyDefinition
            .Create("tenant:config:{tenantId}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("tenantId")
            .WithExpiration(TimeSpan.FromMinutes(30))
            .WithDescription("Tenant-specific configuration")
            .Build();

        /// <summary>
        /// Store share IDs cache.
        /// </summary>
        public static readonly CacheKeyDefinition ShareStores = CacheKeyDefinition
            .Create("share:stores:{id}")
            .WithInstanceKey("id")
            .WithExpiration(CacheExpirations.TwelveHours)
            .WithScope(CacheScope.Tenant)
            .WithDescription("Shared store IDs for data sharing")
            .Build();

        /// <summary>
        /// Store information cache.
        /// </summary>
        public static readonly CacheKeyDefinition StoreInfo = CacheKeyDefinition
            .Create("store:info:{storeId}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("storeId")
            .WithExpiration(TimeSpan.FromHours(12))
            .WithDescription("Store information cache")
            .Build();

        /// <summary>
        /// Patient source list by shared group cache.
        /// </summary>
        public static readonly CacheKeyDefinition PatientSourceListByGroup = CacheKeyDefinition
            .Create("patient-source-list:group:{groupId}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("groupId")
            .WithExpiration(TimeSpan.FromMinutes(30))
            .WithDescription("Patient source list cache by shared group")
            .WithTagGenerator((context, instanceValue) =>
            {
                return new[]
                {
                    $"tenant:{context.TenantId}",
                    "entity:patient-source",
                    $"shared-group:{instanceValue}",
                    $"list:patient-source:group:{instanceValue}"
                };
            })
            .Build();

        /// <summary>
        /// Registers all tenant cache keys with the registry.
        /// Should be called at application startup.
        /// </summary>
        public static void RegisterAll()
        {
            CacheKeyRegistry.Register("Tenant.TenantDbConnection", TenantDbConnection, Category);
            CacheKeyRegistry.Register("Tenant.TenantConfig", TenantConfig, Category);
            CacheKeyRegistry.Register("Tenant.ShareStores", ShareStores, Category);
            CacheKeyRegistry.Register("Tenant.StoreInfo", StoreInfo, Category);
            CacheKeyRegistry.Register("Tenant.PatientSourceListByGroup", PatientSourceListByGroup, Category);
        }
    }
}
