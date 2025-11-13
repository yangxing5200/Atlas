// Tags/Conventions/TagNamingConvention.cs
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Tags.Conventions
{
    public static class TagNamingConvention
    {
        // Entity tags
        public static string Entity(string entityName) => $"entity:{entityName}";
        public static string EntityId(string entityName, object id) => $"entity:{entityName}:{id}";

        // Scope tags
        public static string Tenant(string tenantId) => $"tenant:{tenantId}";
        public static string Store(string tenantId, string storeId) => $"store:{tenantId}:{storeId}";
        public static string User(string tenantId, string userId) => $"user:{tenantId}:{userId}";

        // Feature tags
        public static string Feature(string featureName) => $"feature:{featureName}";
        public static string Module(string moduleName) => $"module:{moduleName}";

        // Relationship tags
        public static string Relationship(string entityName, string relatedEntity)
            => $"rel:{entityName}:{relatedEntity}";
    }
}