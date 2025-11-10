using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Keys;

namespace Atlas.Infrastructure.Caching.Extensions;

/// <summary>
/// 缓存服务扩展方法
/// </summary>
public static class CacheServiceExtensions
{
    /// <summary>
    /// 简化的获取或创建（使用默认配置）
    /// </summary>
    public static Task<T?> GetOrCreateAsync<T>(
        this ICacheService cache,
        string keyName,
        Func<Task<T>> factory) where T : class
    {
        var definition = new CacheKeyDefinition(keyName);
        return cache.GetOrCreateAsync(definition, factory);
    }

    /// <summary>
    /// 带过期时间的简化方法
    /// </summary>
    public static Task<T?> GetOrCreateAsync<T>(
        this ICacheService cache,
        string keyName,
        TimeSpan expiration,
        Func<Task<T>> factory) where T : class
    {
        var definition = new CacheKeyDefinition(keyName, defaultExpiration: expiration);
        return cache.GetOrCreateAsync(definition, factory);
    }

}