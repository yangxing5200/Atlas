namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 缓存键实例（实例态 - 不可变）
/// </summary>
public class CacheKeyInstance
{
    /// <summary>
    /// 键定义
    /// </summary>
    public CacheKeyDefinition Definition { get; }

    /// <summary>
    /// 作用域上下文
    /// </summary>
    public ScopeContext Context { get; }

    /// <summary>
    /// 实例值
    /// </summary>
    public object? InstanceValue { get; }

    /// <summary>
    /// 唯一键（缓存用）
    /// </summary>
    public string UniqueKey { get; }

    /// <summary>
    /// 实际过期时间（包含随机偏移）
    /// </summary>
    public TimeSpan ActualExpiration { get; }

    public CacheKeyInstance(
        CacheKeyDefinition definition,
        ScopeContext context,
        object? instanceValue)
    {
        Definition = definition;
        Context = context;
        InstanceValue = instanceValue;
        UniqueKey = BuildUniqueKey();
        ActualExpiration = CalculateExpiration();
    }

    private string BuildUniqueKey()
    {
        var parts = new List<string> { "Atlas" };

        // 添加作用域部分
        switch (Definition.Scope)
        {
            case CacheKeyScope.Global:
                // 只有前缀
                break;
            case CacheKeyScope.Tenant:
                parts.Add(Context.TenantId?.ToString() ?? "0");
                break;
            case CacheKeyScope.Store:
                parts.Add(Context.TenantId?.ToString() ?? "0");
                parts.Add(Context.StoreId?.ToString() ?? "0");
                break;
            case CacheKeyScope.User:
                parts.Add(Context.TenantId?.ToString() ?? "0");
                parts.Add(Context.StoreId?.ToString() ?? "0");
                parts.Add(Context.UserId?.ToString() ?? "0");
                break;
        }

        // 添加键名
        parts.Add(Definition.Name);

        // 添加实例值
        if (InstanceValue != null)
        {
            parts.Add(InstanceValue.ToString()!);
        }

        return string.Join(":", parts);
    }

    private TimeSpan CalculateExpiration()
    {
        if (Definition.MaxRandomOffsetSeconds <= 0)
            return Definition.DefaultExpiration;

        var random = new Random();
        var offset = random.Next(0, Definition.MaxRandomOffsetSeconds);
        return Definition.DefaultExpiration.Add(TimeSpan.FromSeconds(offset));
    }

    public override string ToString() => UniqueKey;

    public override int GetHashCode() => UniqueKey.GetHashCode();

    public override bool Equals(object? obj)
    {
        return obj is CacheKeyInstance other && UniqueKey == other.UniqueKey;
    }
}