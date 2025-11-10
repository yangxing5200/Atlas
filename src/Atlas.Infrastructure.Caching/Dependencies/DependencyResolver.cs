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

    /// <summary>
    /// 解析需要失效的标签
    /// </summary>
    public async Task<List<string>> ResolveInvalidationTagsAsync(
        IEnumerable<EntityChangeInfo> changes,
        CancellationToken cancellationToken = default)
    {
        var tagsToInvalidate = new HashSet<string>();

        foreach (var change in changes)
        {
            var dependentKeys = _registry.GetDependentKeys(change.EntityType);

            _logger.LogDebug("Found {Count} dependent cache keys for entity type {EntityType}",
                dependentKeys.Count, change.EntityType.Name);

            foreach (var keyDef in dependentKeys)
            {
                var dependency = keyDef.Dependencies.FirstOrDefault(d => d.EntityType == change.EntityType);
                if (dependency == null)
                    continue;

                // 检查是否需要失效
                if (!ShouldInvalidate(dependency, change))
                {
                    _logger.LogTrace("Skipping cache key '{CacheKey}' - property changes do not match triggers",
                        keyDef.Name);
                    continue;
                }

                // 获取当前作用域上下文
                var scopeContext = _keyBuilder.GetCurrentScopeContext(keyDef);

                // 生成带作用域的标签
                var tags = GenerateTagsForDependency(dependency, change, keyDef, scopeContext);
                foreach (var tag in tags)
                {
                    tagsToInvalidate.Add(tag);
                    _logger.LogTrace("Added invalidation tag: {Tag}", tag);
                }
            }
        }

        _logger.LogDebug("Resolved {Count} tags for invalidation: {Tags}",
            tagsToInvalidate.Count, string.Join(", ", tagsToInvalidate));

        return await Task.FromResult(tagsToInvalidate.ToList());
    }

    /// <summary>
    /// 为依赖生成带作用域的标签
    /// </summary>
    private List<string> GenerateTagsForDependency(
        CacheDependency dependency,
        EntityChangeInfo change,
        CacheKeyDefinition keyDef,
        ScopeContext scopeContext)
    {
        var tags = new List<string>();
        var entityTypeName = dependency.EntityType.Name;

        if (dependency.Level == DependencyLevel.Type)
        {
            // Type级别：dependency:EntityName
            var baseTag = $"dependency:{entityTypeName}";
            tags.Add(BuildScopedTag(baseTag, keyDef.Scope, scopeContext));

            // 如果指定了触发属性，也生成属性级别的标签
            if (dependency.TriggerProperties.Count > 0)
            {
                foreach (var property in dependency.TriggerProperties)
                {
                    var propertyTag = $"dependency:{entityTypeName}:{property}";
                    tags.Add(BuildScopedTag(propertyTag, keyDef.Scope, scopeContext));
                }
            }

            _logger.LogTrace("Type-level dependency tags generated for {EntityName}", entityTypeName);
        }
        else if (dependency.Level == DependencyLevel.Instance)
        {
            // Instance级别：尝试生成精确标签
            if (TryExtractInstanceKey(dependency, change, out var instanceKey))
            {
                // 精确实例标签：entity:EntityName:InstanceId
                var baseTag = $"entity:{entityTypeName}:{instanceKey}";
                tags.Add(BuildScopedTag(baseTag, keyDef.Scope, scopeContext));

                // 如果指定了触发属性，也生成属性级别的标签
                if (dependency.TriggerProperties.Count > 0)
                {
                    foreach (var property in dependency.TriggerProperties)
                    {
                        var propertyTag = $"entity:{entityTypeName}:{instanceKey}:{property}";
                        tags.Add(BuildScopedTag(propertyTag, keyDef.Scope, scopeContext));
                    }
                }

                _logger.LogTrace("Instance-level tags generated: entity:{EntityName}:{InstanceKey}",
                    entityTypeName, instanceKey);
            }
            else
            {
                // 无法提取实例键（跨实体依赖）-> 降级为类型标签
                var fallbackTag = $"dependency:{entityTypeName}";
                tags.Add(BuildScopedTag(fallbackTag, keyDef.Scope, scopeContext));

                _logger.LogWarning(
                    "Cannot extract instance key for {EntityType} in cache key '{CacheKeyName}'. " +
                    "Using type-level tag",
                    entityTypeName, keyDef.Name);
            }
        }

        return tags;
    }

    /// <summary>
    /// 构建带作用域前缀的完整标签
    /// </summary>
    /// <remarks>
    /// 标签格式：Atlas:{Scope前缀}:{BasePart}
    /// <para>示例：</para>
    /// <list type="bullet">
    /// <item>Global: Atlas:dependency:Product</item>
    /// <item>Tenant: Atlas:123:dependency:Product</item>
    /// <item>Store: Atlas:123:456:dependency:Product</item>
    /// <item>User: Atlas:123:456:789:dependency:Product</item>
    /// </list>
    /// </remarks>
    private string BuildScopedTag(string basePart, CacheKeyScope scope, ScopeContext context)
    {
        var parts = new List<string>(6);

        // 根据作用域添加前缀（与 CacheKeyInstance.BuildScopedTag 保持一致）
        switch (scope)
        {
            case CacheKeyScope.Global:
                // 全局作用域：无额外前缀
                break;

            case CacheKeyScope.Tenant:
                // 租户级别：TenantId
                if (context.TenantId.HasValue)
                    parts.Add(context.TenantId.Value.ToString());
                break;

            case CacheKeyScope.Store:
                // 店铺级别：TenantId:StoreId
                if (context.TenantId.HasValue)
                    parts.Add(context.TenantId.Value.ToString());
                if (context.StoreId.HasValue)
                    parts.Add(context.StoreId.Value.ToString());
                break;

            case CacheKeyScope.User:
                // 用户级别：TenantId:StoreId:UserId
                if (context.TenantId.HasValue)
                    parts.Add(context.TenantId.Value.ToString());
                if (context.StoreId.HasValue)
                    parts.Add(context.StoreId.Value.ToString());
                if (context.UserId.HasValue)
                    parts.Add(context.UserId.Value.ToString());
                break;
        }

        // 添加基础标签部分
        parts.Add(basePart);

        // 拼接最终标签：Atlas:{parts}
        return $"Atlas:{string.Join(":", parts)}";
    }

    /// <summary>
    /// 尝试提取实例键
    /// </summary>
    private bool TryExtractInstanceKey(
        CacheDependency dependency,
        EntityChangeInfo change,
        out object? instanceKey)
    {
        instanceKey = null;

        if (dependency.InstanceKeySelector == null)
        {
            return false;
        }

        try
        {
            instanceKey = dependency.InstanceKeySelector(change.Entity);
            return instanceKey != null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract instance key for {EntityType}",
                dependency.EntityType.Name);
            return false;
        }
    }

    /// <summary>
    /// 检查是否应该失效缓存
    /// </summary>
    private bool ShouldInvalidate(CacheDependency dependency, EntityChangeInfo change)
    {
        // 如果没有指定触发属性，任何变化都触发
        if (dependency.TriggerProperties.Count == 0)
        {
            _logger.LogTrace("No trigger properties specified - invalidating for any change");
            return true;
        }

        // 新增或删除：总是触发
        if (change.State == EntityChangeState.Added || change.State == EntityChangeState.Deleted)
        {
            _logger.LogTrace("Entity state is {State} - always invalidating", change.State);
            return true;
        }

        // 使用不区分大小写的比较
        var triggerPropertiesLower = new HashSet<string>(
            dependency.TriggerProperties.Select(p => p.ToLowerInvariant()));

        var modifiedPropertiesLower = change.ModifiedProperties
            .Select(p => p.ToLowerInvariant())
            .ToList();

        var hasMatch = modifiedPropertiesLower.Any(p => triggerPropertiesLower.Contains(p));

        if (hasMatch)
        {
            var matchedProperties = modifiedPropertiesLower
                .Where(p => triggerPropertiesLower.Contains(p))
                .ToList();

            _logger.LogDebug(
                "Modified properties {ModifiedProperties} match trigger properties {TriggerProperties}. Matched: {Matched}",
                string.Join(", ", change.ModifiedProperties),
                string.Join(", ", dependency.TriggerProperties),
                string.Join(", ", matchedProperties));
        }
        else
        {
            _logger.LogTrace(
                "Modified properties {ModifiedProperties} do NOT match trigger properties {TriggerProperties}",
                string.Join(", ", change.ModifiedProperties),
                string.Join(", ", dependency.TriggerProperties));
        }

        return hasMatch;
    }

    /// <summary>
    /// 解析需要失效的精确键
    /// </summary>
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