using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Keys;

namespace Atlas.Infrastructure.Caching.Abstractions
{
    /// <summary>
    /// 作用域上下文访问器接口
    /// </summary>
    public interface IScopeContextAccessor
    {
        ScopeContext? Current { get; set; }
        string? TenantId { get; }
        string? StoreId { get; }
        string? UserId { get; }
        bool HasTenant { get; }
        bool HasStore { get; }
        bool HasUser { get; }
    }
}