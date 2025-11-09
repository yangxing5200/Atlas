namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 当前身份标识接口
/// </summary>
public interface ICurrentIdentity
{
    long? TenantId { get; }
    long? StoreId { get; }
    long? UserId { get; }
}