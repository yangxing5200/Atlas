using Atlas.Infrastructure.Caching.Dependencies;

namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 缓存键实例，包含完整的键路径、过期时间和作用域标签
/// </summary>
public class CacheKeyInstance
{
    private readonly CacheKeyDefinition _definition;
    private readonly ScopeContext _context;
    private readonly object? _instanceValue;

    /// <summary>
    /// 完整的缓存键，包含作用域前缀和实例标识
    /// </summary>
    public string UniqueKey { get; }

    /// <summary>
    /// 实际过期时间（含随机偏移）
    /// </summary>
    public TimeSpan ActualExpiration { get; }

    /// <summary>
    /// 实例值，用于依赖解析
    /// </summary>
    public object? InstanceValue => _instanceValue;

    /// <summary>
    /// 标签集合，包含作用域信息用于失效协调
    /// </summary>
    public IReadOnlyList<string> Tags { get; }

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
    /// 构建完整的缓存键路径
    /// </summary>
    /// <remarks>
    /// 键格式：Atlas:{Scope前缀}:{KeyName}:{InstanceValue}
    /// <para>层级结构：</para>
    /// <list type="bullet">
    /// <item>Global: Atlas:KeyName</item>
    /// <item>Tenant: Atlas:TenantId:KeyName</item>
    /// <item>Store: Atlas:TenantId:StoreId:KeyName</item>
    /// <item>User: Atlas:TenantId:StoreId:UserId:KeyName</item>
    /// <item>带实例值: Atlas:...:KeyName:InstanceValue</item>
    /// </list>
    /// </remarks>
    /// <returns>格式化的缓存键字符串</returns>
    private string BuildUniqueKey()
    {
        // 预分配容量避免扩容（通常包含4-6个部分）
        var parts = new List<string>(6);

        // 根据作用域按顺序添加层级前缀
        switch (_definition.Scope)
        {
            case CacheKeyScope.Global:
                // 全局作用域：无前缀
                break;

            case CacheKeyScope.Tenant:
                // 租户级别：TenantId
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                break;

            case CacheKeyScope.Store:
                // 店铺级别：TenantId:StoreId
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                if (_context.StoreId.HasValue)
                    parts.Add(_context.StoreId.Value.ToString());
                break;

            case CacheKeyScope.User:
                // 用户级别：TenantId:StoreId:UserId
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

        // 拼接最终键：Atlas:{parts}
        return $"Atlas:{string.Join(":", parts)}";
    }

    /// <summary>
    /// 计算过期时间，添加随机偏移防止缓存雪崩
    /// </summary>
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
    /// 生成包含作用域信息的依赖标签
    /// </summary>
    /// <remarks>
    /// 标签格式：Atlas:{Scope前缀}:dependency|entity:{EntityType}[:{InstanceKey}][:{Property}]
    /// <para>标签类型：</para>
    /// <list type="bullet">
    /// <item>类型级别: Atlas:123:456:dependency:Product</item>
    /// <item>类型属性: Atlas:123:456:dependency:Product:Price</item>
    /// <item>实例级别: Atlas:123:456:entity:Order:100</item>
    /// <item>实例属性: Atlas:123:456:entity:Order:100:Status</item>
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
                // 类型级别依赖：dependency:EntityType
                tags.Add(BuildScopedTag($"dependency:{entityTypeName}"));

                // 属性级别：dependency:EntityType:PropertyName
                if (dependency.TriggerProperties != null && dependency.TriggerProperties.Count > 0)
                {
                    foreach (var property in dependency.TriggerProperties)
                    {
                        tags.Add(BuildScopedTag($"dependency:{entityTypeName}:{property}"));
                    }
                }
            }
            else if (dependency.Level == DependencyLevel.Instance)
            {
                // 实例级别依赖
                if (TryExtractInstanceKey(dependency, out var instanceKey))
                {
                    // entity:EntityType:InstanceId
                    tags.Add(BuildScopedTag($"entity:{entityTypeName}:{instanceKey}"));

                    // entity:EntityType:InstanceId:PropertyName
                    if (dependency.TriggerProperties != null && dependency.TriggerProperties.Count > 0)
                    {
                        foreach (var property in dependency.TriggerProperties)
                        {
                            tags.Add(BuildScopedTag($"entity:{entityTypeName}:{instanceKey}:{property}"));
                        }
                    }
                }
                else
                {
                    // 无法提取实例键时，降级为类型级别
                    tags.Add(BuildScopedTag($"dependency:{entityTypeName}"));
                }
            }
        }

        return tags;
    }

    /// <summary>
    /// 构建带作用域前缀的完整标签
    /// </summary>
    /// <remarks>
    /// 标签格式与 UniqueKey 保持一致：Atlas:{Scope前缀}:{BasePart}
    /// <para>示例：</para>
    /// <list type="bullet">
    /// <item>Global: Atlas:dependency:Product</item>
    /// <item>Tenant: Atlas:123:dependency:Product</item>
    /// <item>Store: Atlas:123:456:dependency:Product</item>
    /// <item>User: Atlas:123:456:789:dependency:Product</item>
    /// </list>
    /// </remarks>
    /// <param name="basePart">基础标签部分（如 dependency:Product:Price）</param>
    /// <returns>完整的作用域标签</returns>
    private string BuildScopedTag(string basePart)
    {
        // 预分配容量避免扩容
        var parts = new List<string>(6);

        // 按照作用域层级添加前缀（与 BuildUniqueKey 保持一致）
        switch (_definition.Scope)
        {
            case CacheKeyScope.Global:
                // 全局作用域：无额外前缀
                break;

            case CacheKeyScope.Tenant:
                // 租户级别：TenantId
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                break;

            case CacheKeyScope.Store:
                // 店铺级别：TenantId:StoreId
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                if (_context.StoreId.HasValue)
                    parts.Add(_context.StoreId.Value.ToString());
                break;

            case CacheKeyScope.User:
                // 用户级别：TenantId:StoreId:UserId
                if (_context.TenantId.HasValue)
                    parts.Add(_context.TenantId.Value.ToString());
                if (_context.StoreId.HasValue)
                    parts.Add(_context.StoreId.Value.ToString());
                if (_context.UserId.HasValue)
                    parts.Add(_context.UserId.Value.ToString());
                break;
        }

        // 添加基础标签部分
        parts.Add(basePart);

        // 拼接最终标签：Atlas:{parts}
        return $"Atlas:{string.Join(":", parts)}";
    }

    /// <summary>
    /// 尝试通过选择器提取实例键
    /// </summary>
    private bool TryExtractInstanceKey(CacheDependency dependency, out string instanceKey)
    {
        instanceKey = string.Empty;

        if (_instanceValue == null || dependency.InstanceKeySelector == null)
            return false;

        try
        {
            var key = dependency.InstanceKeySelector(_instanceValue);
            if (key != null)
            {
                instanceKey = key.ToString() ?? string.Empty;
                return !string.IsNullOrEmpty(instanceKey);
            }
        }
        catch
        {
            // Selector execution failed
        }

        return false;
    }
}