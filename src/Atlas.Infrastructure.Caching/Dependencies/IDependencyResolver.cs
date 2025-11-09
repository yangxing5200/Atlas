namespace Atlas.Infrastructure.Caching.Dependencies;

/// <summary>
/// 依赖解析器接口
/// </summary>
public interface IDependencyResolver
{
    /// <summary>
    /// 解析需要失效的缓存键
    /// </summary>
    Task<List<string>> ResolveInvalidationKeysAsync(
        IEnumerable<EntityChangeInfo> changes,
        CancellationToken cancellationToken = default);
}