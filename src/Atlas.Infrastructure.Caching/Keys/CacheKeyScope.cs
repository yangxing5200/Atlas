namespace Atlas.Infrastructure.Caching.Keys;

/// <summary>
/// 缓存键作用域
/// </summary>
public enum CacheKeyScope
{
    /// <summary>
    /// 全局作用域：Atlas:KeyName
    /// </summary>
    Global,

    /// <summary>
    /// 租户作用域：Atlas:{TenantId}:KeyName
    /// </summary>
    Tenant,

    /// <summary>
    /// 门店作用域：Atlas:{TenantId}:{StoreId}:KeyName
    /// </summary>
    Store,

    /// <summary>
    /// 用户作用域：Atlas:{TenantId}:{StoreId}:{UserId}:KeyName
    /// </summary>
    User
}