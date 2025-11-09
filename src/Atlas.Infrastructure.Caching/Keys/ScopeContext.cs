namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 作用域上下文（上下文态）
/// </summary>
public class ScopeContext
{
    public long? TenantId { get; set; }
    public long? StoreId { get; set; }
    public long? UserId { get; set; }
}