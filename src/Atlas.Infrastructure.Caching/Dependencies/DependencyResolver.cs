using Atlas.Infrastructure.Caching.Keys;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Dependencies;

public class DependencyResolver : IDependencyResolver
{
    private readonly DependencyRegistry _registry;
    private readonly ICacheKeyBuilder _keyBuilder;
    private readonly ILogger<DependencyResolver> _logger;

    public DependencyResolver(
        DependencyRegistry registry,
        ICacheKeyBuilder keyBuilder,
        ILogger<DependencyResolver> logger)
    {
        _registry = registry;
        _keyBuilder = keyBuilder;
        _logger = logger;
    }

    public async Task<List<string>> ResolveInvalidationKeysAsync(
        IEnumerable<EntityChangeInfo> changes,
        CancellationToken cancellationToken = default)
    {
        var keysToInvalidate = new HashSet<string>();

        foreach (var change in changes)
        {
            var dependentKeys = _registry.GetDependentKeys(change.EntityType);

            foreach (var keyDef in dependentKeys)
            {
                var dependency = keyDef.Dependencies.FirstOrDefault(d => d.EntityType == change.EntityType);
                if (dependency == null)
                    continue;

                // 检查是否需要失效
                if (!ShouldInvalidate(dependency, change))
                    continue;

                // Type级别依赖
                if (dependency.Level == DependencyLevel.Type)
                {
                    var keyInstance = _keyBuilder.Build(keyDef);
                    keysToInvalidate.Add(keyInstance.UniqueKey + "*"); // 模式匹配
                }
                // Instance级别依赖
                else if (dependency.Level == DependencyLevel.Instance)
                {
                    var instanceKeys = ExtractInstanceKeys(dependency, change);
                    foreach (var instanceKey in instanceKeys)
                    {
                        var keyInstance = _keyBuilder.Build(keyDef, instanceKey);
                        keysToInvalidate.Add(keyInstance.UniqueKey);
                    }
                }
            }
        }

        return await Task.FromResult(keysToInvalidate.ToList());
    }

    private bool ShouldInvalidate(CacheDependency dependency, EntityChangeInfo change)
    {
        // 如果没有指定触发属性，任何变化都触发
        if (dependency.TriggerProperties.Count == 0)
            return true;

        // 检查变更的属性是否在触发属性列表中
        return change.ModifiedProperties.Any(p => dependency.TriggerProperties.Contains(p));
    }

    private List<object> ExtractInstanceKeys(CacheDependency dependency, EntityChangeInfo change)
    {
        var keys = new List<object>();

        if (dependency.InstanceKeySelector == null)
        {
            _logger.LogWarning(
                "Instance level dependency for {EntityType} has no InstanceKeySelector",
                dependency.EntityType.Name);
            return keys;
        }

        try
        {
            // 提取新值
            var newKey = dependency.InstanceKeySelector(change.Entity);
            if (newKey != null)
                keys.Add(newKey);

            // 如果是修改，还需要提取旧值
            if (change.State == EntityChangeState.Modified && change.OldValues.Count > 0)
            {
                // 尝试从旧值重建实例（简化处理）
                // 实际实现可能需要更复杂的逻辑
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract instance key for {EntityType}", dependency.EntityType.Name);
        }

        return keys;
    }
}