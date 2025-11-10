using Atlas.Infrastructure.Caching.Keys;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Caching.Dependencies;

/// <summary>
/// 依赖解析器，负责将实体变化转换为缓存失效标签
/// </summary>
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
    /// 解析实体变化产生的失效标签
    /// </summary>
    /// <param name="changes">实体变化列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>需要失效的标签列表（去重）</returns>
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

                // 检查属性变化是否触发失效
                if (!ShouldInvalidate(dependency, change))
                {
                    _logger.LogTrace("Skipping cache key '{CacheKey}' - property changes do not match triggers",
                        keyDef.Name);
                    continue;
                }

                // 获取当前作用域上下文
                var scopeContext = _keyBuilder.GetCurrentScopeContext(keyDef);

                // 生成失效标签
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
    /// 为单个依赖生成失效标签
    /// </summary>
    /// <param name="dependency">依赖定义</param>
    /// <param name="change">实体变化信息</param>
    /// <param name="keyDef">缓存键定义</param>
    /// <param name="scopeContext">作用域上下文</param>
    /// <returns>生成的标签列表</returns>
    /// <remarks>
    /// <para>生成策略：</para>
    /// <list type="bullet">
    /// <item><description>类型级依赖：生成 dependency:EntityType[:Property] 标签</description></item>
    /// <item><description>实例级依赖：生成 entity:EntityType:InstanceKey[:Property] 标签</description></item>
    /// <item><description>只生成实际变化且在触发列表中的属性标签</description></item>
    /// </list>
    /// </remarks>
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
            GenerateTypeLevelInvalidationTags(tags, dependency, change, entityTypeName, keyDef, scopeContext);
        }
        else if (dependency.Level == DependencyLevel.Instance)
        {
            GenerateInstanceLevelInvalidationTags(tags, dependency, change, entityTypeName, keyDef, scopeContext);
        }

        return tags;
    }

    /// <summary>
    /// 生成类型级失效标签
    /// </summary>
    private void GenerateTypeLevelInvalidationTags(
        List<string> tags,
        CacheDependency dependency,
        EntityChangeInfo change,
        string entityTypeName,
        CacheKeyDefinition keyDef,
        ScopeContext scopeContext)
    {
        if (dependency.TriggerProperties.Count > 0)
        {
            // 精确失效：只生成实际变化的属性标签
            var matchedProperties = new List<string>();

            foreach (var property in dependency.TriggerProperties)
            {
                if (change.ModifiedProperties.Any(p =>
                    string.Equals(p, property, StringComparison.OrdinalIgnoreCase)))
                {
                    var propertyTag = $"dependency:{entityTypeName}:{property}";
                    tags.Add(BuildScopedTag(propertyTag, keyDef.Scope, scopeContext));
                    matchedProperties.Add(property);
                }
            }

            if (matchedProperties.Count > 0)
            {
                _logger.LogDebug(
                    "Generated {Count} type-property tags for {EntityType}. Matched: {Properties}",
                    tags.Count, entityTypeName, string.Join(", ", matchedProperties));
            }
            else
            {
                _logger.LogTrace(
                    "No matching property changes for {EntityType}. " +
                    "Modified: [{Modified}], Triggers: [{Triggers}]",
                    entityTypeName,
                    string.Join(", ", change.ModifiedProperties),
                    string.Join(", ", dependency.TriggerProperties));
            }
        }
        else
        {
            // 宽松失效：任何变化都生成类型标签
            var baseTag = $"dependency:{entityTypeName}";
            tags.Add(BuildScopedTag(baseTag, keyDef.Scope, scopeContext));

            _logger.LogDebug(
                "Generated type-level tag for {EntityType} (no trigger properties)",
                entityTypeName);
        }
    }

    /// <summary>
    /// 生成实例级失效标签
    /// </summary>
    private void GenerateInstanceLevelInvalidationTags(
        List<string> tags,
        CacheDependency dependency,
        EntityChangeInfo change,
        string entityTypeName,
        CacheKeyDefinition keyDef,
        ScopeContext scopeContext)
    {
        if (TryExtractInstanceKey(dependency, change, out var instanceKey))
        {
            // 成功提取实例键：生成实例级标签
            if (dependency.TriggerProperties.Count > 0)
            {
                // 精确失效：只生成实际变化的属性标签
                var matchedProperties = new List<string>();

                foreach (var property in dependency.TriggerProperties)
                {
                    if (change.ModifiedProperties.Any(p =>
                        string.Equals(p, property, StringComparison.OrdinalIgnoreCase)))
                    {
                        var propertyTag = $"entity:{entityTypeName}:{instanceKey}:{property}";
                        tags.Add(BuildScopedTag(propertyTag, keyDef.Scope, scopeContext));
                        matchedProperties.Add(property);
                    }
                }

                if (matchedProperties.Count > 0)
                {
                    _logger.LogDebug(
                        "Generated {Count} instance-property tags for {EntityType}:{InstanceKey}. Matched: {Properties}",
                        tags.Count, entityTypeName, instanceKey, string.Join(", ", matchedProperties));
                }
            }
            else
            {
                // 宽松失效：任何属性变化都生成实例标签
                var baseTag = $"entity:{entityTypeName}:{instanceKey}";
                tags.Add(BuildScopedTag(baseTag, keyDef.Scope, scopeContext));

                _logger.LogDebug(
                    "Generated instance-level tag for {EntityType}:{InstanceKey}",
                    entityTypeName, instanceKey);
            }
        }
        else
        {
            // 无法提取实例键：区分处理
            HandleInstanceKeyExtractionFailure(dependency, change, entityTypeName, keyDef);
        }
    }

    /// <summary>
    /// 处理实例键提取失败的情况
    /// </summary>
    /// <remarks>
    /// <para>失败原因分析：</para>
    /// <list type="bullet">
    /// <item><description>同实体依赖：可能是配置错误（InstanceKeySelector 缺失或异常）</description></item>
    /// <item><description>跨实体依赖：预期行为（从关联实体无法提取目标实体的键）</description></item>
    /// </list>
    /// <para>处理策略：</para>
    /// <list type="bullet">
    /// <item><description>同实体：记录错误，不生成标签（避免过度失效）</description></item>
    /// <item><description>跨实体：记录跟踪日志，不生成标签（符合预期）</description></item>
    /// </list>
    /// </remarks>
    private void HandleInstanceKeyExtractionFailure(
        CacheDependency dependency,
        EntityChangeInfo change,
        string entityTypeName,
        CacheKeyDefinition keyDef)
    {
        var isSameEntity = dependency.EntityType == change.EntityType;

        if (isSameEntity)
        {
            // 同实体但无法提取键：配置错误
            _logger.LogError(
                "Failed to extract instance key for same-entity dependency {EntityType} " +
                "in cache key '{CacheKeyName}'. InstanceKeySelector: {HasSelector}. " +
                "This may cause invalidation to be skipped.",
                entityTypeName,
                keyDef.Name,
                dependency.InstanceKeySelector != null);
        }
        else
        {
            // 跨实体依赖：预期行为
            _logger.LogTrace(
                "Cross-entity instance dependency: cache key '{CacheKeyName}' depends on {DependencyType}, " +
                "but change is from {ChangeType}. Skipping (expected behavior).",
                keyDef.Name, entityTypeName, change.EntityType.Name);
        }
    }

    /// <summary>
    /// 构建带作用域的完整标签
    /// </summary>
    /// <param name="basePart">基础标签部分</param>
    /// <param name="scope">作用域级别</param>
    /// <param name="context">作用域上下文</param>
    /// <returns>完整标签</returns>
    private string BuildScopedTag(string basePart, CacheKeyScope scope, ScopeContext context)
    {
        var parts = new List<string>(6);

        // 按作用域添加层级前缀
        switch (scope)
        {
            case CacheKeyScope.Global:
                break;

            case CacheKeyScope.Tenant:
                if (context.TenantId.HasValue)
                    parts.Add(context.TenantId.Value.ToString());
                break;

            case CacheKeyScope.Store:
                if (context.TenantId.HasValue)
                    parts.Add(context.TenantId.Value.ToString());
                if (context.StoreId.HasValue)
                    parts.Add(context.StoreId.Value.ToString());
                break;

            case CacheKeyScope.User:
                if (context.TenantId.HasValue)
                    parts.Add(context.TenantId.Value.ToString());
                if (context.StoreId.HasValue)
                    parts.Add(context.StoreId.Value.ToString());
                if (context.UserId.HasValue)
                    parts.Add(context.UserId.Value.ToString());
                break;
        }

        parts.Add(basePart);

        return $"Atlas:{string.Join(":", parts)}";
    }

    /// <summary>
    /// 尝试从实体对象中提取实例键
    /// </summary>
    /// <param name="dependency">依赖定义</param>
    /// <param name="change">实体变化信息</param>
    /// <param name="instanceKey">输出的实例键</param>
    /// <returns>是否成功提取</returns>
    /// <remarks>
    /// 通过 InstanceKeySelector 委托从完整的实体对象中提取键值
    /// </remarks>
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
    /// 判断实体变化是否应触发缓存失效
    /// </summary>
    /// <param name="dependency">依赖定义</param>
    /// <param name="change">实体变化信息</param>
    /// <returns>是否应失效</returns>
    /// <remarks>
    /// <para>失效规则：</para>
    /// <list type="number">
    /// <item><description>未指定触发属性：任何变化都触发</description></item>
    /// <item><description>新增或删除操作：总是触发</description></item>
    /// <item><description>修改操作：仅当变化属性在触发列表中时触发</description></item>
    /// </list>
    /// </remarks>
    private bool ShouldInvalidate(CacheDependency dependency, EntityChangeInfo change)
    {
        // 规则1：未指定触发属性，任何变化都触发
        if (dependency.TriggerProperties.Count == 0)
        {
            _logger.LogTrace("No trigger properties specified - invalidating for any change");
            return true;
        }

        // 规则2：新增或删除总是触发
        if (change.State == EntityChangeState.Added || change.State == EntityChangeState.Deleted)
        {
            _logger.LogTrace("Entity state is {State} - always invalidating", change.State);
            return true;
        }

        // 规则3：检查修改的属性是否在触发列表中（不区分大小写）
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
    /// 解析需要失效的精确缓存键
    /// </summary>
    /// <param name="changes">实体变化列表</param>
    /// <param name="cancellationToken">取消令牌</param>
    /// <returns>需要失效的缓存键列表</returns>
    /// <remarks>
    /// 此方法生成完整的缓存键路径，支持模式匹配（*）和精确匹配
    /// </remarks>
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

                if (!ShouldInvalidate(dependency, change))
                    continue;

                if (dependency.Level == DependencyLevel.Type)
                {
                    // 类型级依赖：使用模式匹配
                    var keyInstance = _keyBuilder.Build(keyDef);
                    keysToInvalidate.Add(keyInstance.UniqueKey + "*");
                }
                else if (dependency.Level == DependencyLevel.Instance)
                {
                    // 实例级依赖：精确键
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

    /// <summary>
    /// 从实体变化中提取所有相关的实例键
    /// </summary>
    /// <param name="dependency">依赖定义</param>
    /// <param name="change">实体变化信息</param>
    /// <returns>实例键列表</returns>
    /// <remarks>
    /// 对于修改操作，可能需要提取变化前后的键值（如外键变更）
    /// </remarks>
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
            // 提取当前值
            var newKey = dependency.InstanceKeySelector(change.Entity);
            if (newKey != null)
                keys.Add(newKey);

            // TODO: 提取旧值（用于处理外键变更等场景）
            // 如果是修改操作且涉及键值变更，需要同时失效新旧两个键对应的缓存
            if (change.State == EntityChangeState.Modified && change.OldValues.Count > 0)
            {
                // 从 OldValues 重建实例并提取旧键
                // 具体实现取决于业务需求
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to extract instance key for {EntityType}", dependency.EntityType.Name);
        }

        return keys;
    }
}