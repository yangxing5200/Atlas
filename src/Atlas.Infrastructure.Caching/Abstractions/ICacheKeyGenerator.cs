using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// 缓存Key生成器接口
    /// </summary>
    public interface ICacheKeyGenerator
    {
        string GenerateKey(string baseKey, CacheScope scope, IDictionary<string, string>? scopeValues = null);
        string GenerateGlobalKey(string baseKey);
        string GenerateTenantKey(string baseKey, string tenantId);
        string GenerateStoreKey(string baseKey, string tenantId, string storeId);
        string GenerateUserKey(string baseKey, string tenantId, string userId);
    }
}