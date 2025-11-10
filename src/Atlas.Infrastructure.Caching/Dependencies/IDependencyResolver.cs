namespace Atlas.Infrastructure.Caching.Dependencies;

public interface IDependencyResolver
{
    /// <summary>
    /// 解析需要失效的标签（推荐）
    /// </summary>
    Task<List<string>> ResolveInvalidationTagsAsync(
        IEnumerable<EntityChangeInfo> changes,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// 解析需要失效的键
    /// </summary>
    Task<List<string>> ResolveInvalidationKeysAsync(
        IEnumerable<EntityChangeInfo> changes,
        CancellationToken cancellationToken = default);
}
