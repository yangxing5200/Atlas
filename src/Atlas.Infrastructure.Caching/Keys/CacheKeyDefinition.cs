using Atlas.Infrastructure.Caching.Dependencies;

namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 缓存键定义（定义态）
/// </summary>
public class CacheKeyDefinition
{
    /// <summary>
    /// 键名称
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// 作用域
    /// </summary>
    public CacheKeyScope Scope { get; }

    /// <summary>
    /// 实例键名称（如果需要实例化）
    /// </summary>
    public string? InstanceKeyName { get; }

    /// <summary>
    /// 默认过期时间
    /// </summary>
    public TimeSpan DefaultExpiration { get; }

    /// <summary>
    /// 依赖配置列表
    /// </summary>
    public List<CacheDependency> Dependencies { get; }

    /// <summary>
    /// 是否启用L1缓存
    /// </summary>
    public bool EnableL1Cache { get; }
    public int MaxRandomOffsetSeconds { get; }

    // 标签生成器
    /// <summary>
    /// 标签生成器：根据上下文和实例值生成标签
    /// </summary>
    public Func<ScopeContext, object?, IEnumerable<string>>? TagGenerator { get; set; }

    public CacheKeyDefinition(
        string name,
        CacheKeyScope scope = CacheKeyScope.Tenant,
        string? instanceKeyName = null,
        TimeSpan? defaultExpiration = null,
        bool enableL1Cache = true,
        int maxRandomOffsetSeconds = 300)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Key name cannot be empty", nameof(name));

        Name = name;
        Scope = scope;
        InstanceKeyName = instanceKeyName;
        DefaultExpiration = defaultExpiration ?? TimeSpan.FromHours(1);
        EnableL1Cache = enableL1Cache;
        MaxRandomOffsetSeconds = maxRandomOffsetSeconds;
        Dependencies = new List<CacheDependency>();
    }

    /// <summary>
    /// 添加依赖
    /// </summary>
    public CacheKeyDefinition DependsOn<TEntity>(
        DependencyLevel level,
        Func<TEntity, object>? instanceKeySelector = null,
        params string[] triggerProperties)
    {
        var dependency = new CacheDependency
        {
            EntityType = typeof(TEntity),
            Level = level,
            InstanceKeySelector = instanceKeySelector != null
                ? obj => instanceKeySelector((TEntity)obj)
                : null
        };

        // 使用内部方法添加属性名
        dependency.AddTriggerProperties(triggerProperties);
        Dependencies.Add(dependency);
        return this;
    }
}