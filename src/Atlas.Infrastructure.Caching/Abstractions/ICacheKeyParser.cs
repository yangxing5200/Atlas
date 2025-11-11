using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Keys.Parsers;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// 缓存Key解析器接口
    /// </summary>
    public interface ICacheKeyParser
    {
        KeyMetadata Parse(string key);
        bool TryParse(string key, out KeyMetadata? metadata);
        CacheScope ExtractScope(string key);
        string? ExtractTenantId(string key);
        string? ExtractStoreId(string key);
        string? ExtractUserId(string key);
    }
}