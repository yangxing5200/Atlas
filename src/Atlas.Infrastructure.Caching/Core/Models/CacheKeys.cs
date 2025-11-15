using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Infrastructure.Caching.Core.Models
{
    public static class CacheKeys
    {
        #region 基础通用缓存键
        /// <summary>
        /// 共享数据的门店（单占位符：id）
        /// </summary>
        public static readonly CacheKeyDefinition Store_ShareIds = CacheKeyDefinition
            .Create("store:share:ids:{id}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("id")
            .WithExpiration(CacheExpirations.OneDay)
            .WithTagGenerator((ctx, instance) => new[] { "store_share_ids" })
            .WithDescription("共享数据的门店id")
            .Build();

        #endregion

    }

    #region 常用过期时间常量（统一管理）
    /// <summary>
    /// 常用缓存过期时间常量
    /// </summary>
    public static class CacheExpirations
    {
        public static readonly TimeSpan OneMinute = TimeSpan.FromMinutes(1);
        public static readonly TimeSpan ThreeMinutes = TimeSpan.FromMinutes(3);
        public static readonly TimeSpan FiveMinutes = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan TenMinutes = TimeSpan.FromMinutes(10);
        public static readonly TimeSpan ThirtyMinutes = TimeSpan.FromMinutes(30);
        public static readonly TimeSpan OneHour = TimeSpan.FromHours(1);
        public static readonly TimeSpan SixHours = TimeSpan.FromHours(6);
        public static readonly TimeSpan TwelveHours = TimeSpan.FromHours(12);
        public static readonly TimeSpan OneDay = TimeSpan.FromDays(1);
        public static readonly TimeSpan SevenDays = TimeSpan.FromDays(7);
    }
    #endregion
}
