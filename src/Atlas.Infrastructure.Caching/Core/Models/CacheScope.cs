namespace Atlas.Infrastructure.Caching.Core.Models
{
    /// <summary>
    /// 缓存作用域枚举
    /// </summary>
    public enum CacheScope
    {
        /// <summary>全局作用域</summary>
        Global = 0,

        /// <summary>租户作用域</summary>
        Tenant = 1,

        /// <summary>门店作用域</summary>
        Store = 2,

        /// <summary>用户作用域</summary>
        User = 3
    }
}