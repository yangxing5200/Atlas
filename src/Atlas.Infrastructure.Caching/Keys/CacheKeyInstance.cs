using Atlas.Infrastructure.Caching.Dependencies;

namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 缓存键实例，封装完整的缓存键路径、过期策略和依赖标签
/// </summary>
public class CacheKeyInstance
{
    private readonly CacheKeyDefinition _definition;
    private readonly ScopeContext _context;
    private readonly object? _instanceValue;

    /// <summary>
    /// 完整的缓存键，格式：Atlas:{TenantId}:{StoreId}:{UserId}:{KeyName}:{InstanceValue}
    /// </summary>
    public string UniqueKey { get; }

    /// <summary>
    /// 实际过期时间（基础TTL + 随机偏移）
    /// </summary>
    public TimeSpan ActualExpiration { get; }

    /// <summary>
    /// 缓存键的实例值（如商品ID、用户ID等）
    /// </summary>
    public object? InstanceValue => _instanceValue;

    /// <summary>
    /// 依赖标签集合，用于失效时的精确匹配
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

    /// <summary>
    /// 初始化缓存键实例
    /// </summary>
    /// <param name="definition">缓存键定义</param>
    /// <param name="context">作用域上下文</param>
    /// <param name="instanceValue">实例值（可选）</param>
    public CacheKeyInstance(
        CacheKeyDefinition definition,
        ScopeContext context,
        object? instanceValue = null)
    {
        _definition = definition ?? throw new ArgumentNullException(nameof(definition));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _instanceValue = instanceValue;

        UniqueKey = BuildUniqueKey();
        ActualExpiration = CalculateExpiration();
        Tags = GenerateTags().ToList();
    }

    /// <summary>
    /// 构建唯一缓存键
    /// </summary>
    /// <remarks>
    /// <para>键格式规则：Atlas:{Scope层级}:{KeyName}[:{InstanceValue}]</para>
    /// <para>层级结构：</para>
    /// <list type="bullet">
    /// <item><description>Global: Atlas:KeyName</description></item>
    /// <item><description>Tenant: Atlas:{TenantId}:KeyName</description></item>
    /// <item><description>Store: Atlas:{TenantId}:{StoreId}:KeyName</description></item>
    /// <item><description>User: Atlas:{TenantId}:{StoreId}:{UserId}:KeyName</description></item>
    /// </list>
    /// </remarks>
    private string BuildUniqueKey()
    {
        var parts = new List<string>(6);

        // 按作用域添加层级前缀
        switch (_definition.Scope)
        {
            case CacheKeyScope.Global:
                break;

            case CacheKeyScope.Tenant:
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                break;

            case CacheKeyScope.Store:
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                if (_context.StoreId.HasValue)
                    parts.Add(_context.StoreId.Value.ToString());
                break;

            case CacheKeyScope.User:
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                if (_context.StoreId.HasValue)
                    parts.Add(_context.StoreId.Value.ToString());
                if (_context.UserId.HasValue)
                    parts.Add(_context.UserId.Value.ToString());
                break;
        }

        // 添加键名
        parts.Add(_definition.Name);

        // 添加实例值（如果存在）
        if (_instanceValue != null && !string.IsNullOrEmpty(_definition.InstanceKeyName))
        {
            parts.Add(_instanceValue.ToString()!);
        }

        return $"Atlas:{string.Join(":", parts)}";
    }

    /// <summary>
    /// 计算实际过期时间
    /// </summary>
    /// <remarks>
    /// 在基础TTL上添加随机偏移，防止大量缓存同时过期导致缓存雪崩
    /// </remarks>
    private TimeSpan CalculateExpiration()
    {
        var expiration = _definition.DefaultExpiration;

        if (_definition.MaxRandomOffsetSeconds > 0)
        {
            var random = new Random();
            var offsetSeconds = random.Next(0, _definition.MaxRandomOffsetSeconds);
            expiration = expiration.Add(TimeSpan.FromSeconds(offsetSeconds));
        }

        return expiration;
    }

    /// <summary>
    /// 生成依赖标签
    /// </summary>
    /// <remarks>
    /// <para>标签格式：Atlas:{Scope层级}:{TagType}:{EntityType}[:{InstanceKey}][:{Property}]</para>
    /// <para>标签类型：</para>
    /// <list type="bullet">
    /// <item><description>dependency - 类型级依赖（关注整个实体类型）</description></item>
    /// <item><description>entity - 实例级依赖（关注特定实例）</description></item>
    /// </list>
    /// <para>标签示例：</para>
    /// <list type="bullet">
    /// <item><description>类型标签: Atlas:123:dependency:Product</description></item>
    /// <item><description>类型属性标签: Atlas:123:dependency:Product:Price</description></item>
    /// <item><description>实例标签: Atlas:123:entity:Product:100</description></item>
    /// <item><description>实例属性标签: Atlas:123:entity:Product:100:Price</description></item>
    /// </list>
    /// </remarks>
    private IEnumerable<string> GenerateTags()
    {
        var tags = new HashSet<string>();

        if (_definition.Dependencies == null || _definition.Dependencies.Count == 0)
        {
            return tags;
        }

        foreach (var dependency in _definition.Dependencies)
        {
            var entityTypeName = dependency.EntityType.Name;

            if (dependency.Level == DependencyLevel.Type)
            {
                GenerateTypeLevelTags(tags, entityTypeName, dependency);
            }
            else if (dependency.Level == DependencyLevel.Instance)
            {
                GenerateInstanceLevelTags(tags, entityTypeName, dependency);
            }
        }

        return tags;
    }

    /// <summary>
    /// 生成类型级依赖标签
    /// </summary>
    /// <param name="tags">标签集合</param>
    /// <param name="entityTypeName">实体类型名</param>
    /// <param name="dependency">依赖定义</param>
    private void GenerateTypeLevelTags(
        HashSet<string> tags,
        string entityTypeName,
        CacheDependency dependency)
    {
        if (dependency.TriggerProperties != null && dependency.TriggerProperties.Count > 0)
        {
            // 精确失效策略：只生成指定属性的标签
            foreach (var property in dependency.TriggerProperties)
            {
                tags.Add(BuildScopedTag($"dependency:{entityTypeName}:{property}"));
            }
        }
        else
        {
            // 宽松失效策略：任何属性变化都触发失效
            tags.Add(BuildScopedTag($"dependency:{entityTypeName}"));
        }
    }

    /// <summary>
    /// 生成实例级依赖标签
    /// </summary>
    /// <param name="tags">标签集合</param>
    /// <param name="entityTypeName">实体类型名</param>
    /// <param name="dependency">依赖定义</param>
    /// <remarks>
    /// <para>处理逻辑：</para>
    /// <list type="number">
    /// <item><description>优先使用缓存键自身的实例值（_instanceValue）</description></item>
    /// <item><description>如果缓存键无实例值，降级为类型级标签</description></item>
    /// </list>
    /// </remarks>
    private void GenerateInstanceLevelTags(
        HashSet<string> tags,
        string entityTypeName,
        CacheDependency dependency)
    {
        if (_instanceValue != null && !string.IsNullOrEmpty(_definition.InstanceKeyName))
        {
            // 缓存键有实例值：生成实例级标签
            var instanceKey = _instanceValue.ToString()!;

            if (dependency.TriggerProperties != null && dependency.TriggerProperties.Count > 0)
            {
                // 精确失效：只生成指定属性的实例标签
                foreach (var property in dependency.TriggerProperties)
                {
                    tags.Add(BuildScopedTag($"entity:{entityTypeName}:{instanceKey}:{property}"));
                }
            }
            else
            {
                // 宽松失效：生成实例基础标签
                tags.Add(BuildScopedTag($"entity:{entityTypeName}:{instanceKey}"));
            }
        }
        else
        {
            // 缓存键无实例值：降级为类型级标签
            // 注意：这种情况通常表示配置不当（实例级依赖应配置在实例级缓存键上）
            if (dependency.TriggerProperties != null && dependency.TriggerProperties.Count > 0)
            {
                foreach (var property in dependency.TriggerProperties)
                {
                    tags.Add(BuildScopedTag($"dependency:{entityTypeName}:{property}"));
                }
            }
            else
            {
                tags.Add(BuildScopedTag($"dependency:{entityTypeName}"));
            }
        }
    }

    /// <summary>
    /// 构建带作用域的完整标签
    /// </summary>
    /// <param name="basePart">基础标签部分（如 dependency:Product:Price）</param>
    /// <returns>完整标签（如 Atlas:123:456:dependency:Product:Price）</returns>
    /// <remarks>
    /// 标签层级与缓存键层级保持一致，确保作用域隔离
    /// </remarks>
    private string BuildScopedTag(string basePart)
    {
        var parts = new List<string>(6);

        // 按作用域添加层级前缀（与 BuildUniqueKey 保持一致）
        switch (_definition.Scope)
        {
            case CacheKeyScope.Global:
                break;

            case CacheKeyScope.Tenant:
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                break;

            case CacheKeyScope.Store:
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                if (_context.StoreId.HasValue)
                    parts.Add(_context.StoreId.Value.ToString());
                break;

            case CacheKeyScope.User:
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                if (_context.StoreId.HasValue)
                    parts.Add(_context.StoreId.Value.ToString());
                if (_context.UserId.HasValue)
                    parts.Add(_context.UserId.Value.ToString());
                break;
        }

        parts.Add(basePart);

        return $"Atlas:{string.Join(":", parts)}";
    }
}