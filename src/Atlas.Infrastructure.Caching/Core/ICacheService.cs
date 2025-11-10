using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Metrics;

namespace Atlas.Infrastructure.Caching.Core;

/// <summary>
/// 缓存服务统一接口
/// </summary>
public interface ICacheService
{
    /// <summary>
    /// 获取或创建缓存（异步）
    /// </summary>
    Task<T?> GetOrCreateAsync<T>(
        CacheKeyDefinition definition,
        Func<Task<T>> factory,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 获取或创建缓存（同步）
    /// </summary>
    T? GetOrCreate<T>(
        CacheKeyDefinition definition,
        Func<T> factory,
        object? instanceValue = null) where T : class;

    /// <summary>
    /// 批量获取或创建
    /// </summary>
    Task<Dictionary<string, T>> GetOrCreateManyAsync<T>(
        CacheKeyDefinition definition,
        IEnumerable<object> instanceValues,
        Func<IEnumerable<object>, Task<Dictionary<object, T>>> bulkFactory,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 设置缓存
    /// </summary>
    Task SetAsync<T>(
        CacheKeyDefinition definition,
        T value,
        object? instanceValue = null,
        CancellationToken cancellationToken = default) where T : class;

    /// <summary>
    /// 移除缓存
    /// </summary>
    Task RemoveAsync(
        CacheKeyDefinition definition,
        object? instanceValue = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 按模式移除
    /// </summary>
    Task RemoveByPatternAsync(
        string pattern,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 清空所有缓存
    /// </summary>
    Task ClearAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// 获取统计信息
    /// </summary>
    Task<CacheStatistics> GetStatisticsAsync(CancellationToken cancellationToken = default);

    //Task<long> InvalidateByEntityTypeAsync<TEntity>(
    //    CancellationToken cancellationToken = default)
    //    where TEntity : class;
}