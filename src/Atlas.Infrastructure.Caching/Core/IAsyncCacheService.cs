using Atlas.Infrastructure.Caching.Keys;

namespace Atlas.Infrastructure.Caching.Core;

/// <summary>
/// 异步缓存服务接口
/// </summary>
public interface IAsyncCacheService
{
    Task<T?> GetOrCreateAsync<T>(
        CacheKeyDefinition definition,
        Func<Task<T>> factory,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class;

    Task<Dictionary<string, T>> GetOrCreateManyAsync<T>(
        CacheKeyDefinition definition,
        IEnumerable<object> instanceValues,
        Func<IEnumerable<object>, Task<Dictionary<object, T>>> bulkFactory,
        CancellationToken cancellationToken = default) where T : class;

    Task SetAsync<T>(
        CacheKeyDefinition definition,
        T value,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class;

    Task RemoveAsync(
        CacheKeyDefinition definition,
        object? instanceValue = null,
        CancellationToken cancellationToken = default);

    Task RemoveByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default);
}