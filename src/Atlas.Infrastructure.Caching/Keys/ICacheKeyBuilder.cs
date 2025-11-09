namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 缓存键构建器接口
/// </summary>
public interface ICacheKeyBuilder
{
    /// <summary>
    /// 构建键实例
    /// </summary>
    CacheKeyInstance Build(CacheKeyDefinition definition, object? instanceValue = null);

    /// <summary>
    /// 批量构建键实例
    /// </summary>
    IEnumerable<CacheKeyInstance> BuildMany(CacheKeyDefinition definition, IEnumerable<object> instanceValues);
}