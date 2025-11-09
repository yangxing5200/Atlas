using Atlas.Infrastructure.Caching.Keys;

namespace Atlas.Infrastructure.Caching.Core;

/// <summary>
/// 同步缓存服务接口（便利包装）
/// </summary>
public interface ISyncCacheService
{
    T? GetOrCreate<T>(
        CacheKeyDefinition definition,
        Func<T> factory,
        object? instanceValue = null) where T : class;

    void Set<T>(
        CacheKeyDefinition definition,
        T value,
        object? instanceValue = null) where T : class;

    void Remove(
        CacheKeyDefinition definition,
        object? instanceValue = null);

    void RemoveByPattern(string pattern);
}