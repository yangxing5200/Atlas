using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Integration.Tests
{
    /// <summary>
    /// 全局缓存键定义（集中管理所有缓存键，消除魔法字符串）
    /// 按业务模块分类，确保全局唯一性
    /// </summary>
    public static class TestCacheKeys
    {
        #region 基础通用缓存键
        /// <summary>
        /// 商品基础信息（单占位符：id）
        /// </summary>
        public static readonly CacheKeyDefinition Product_Base = CacheKeyDefinition
            .Create("product:{id}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("id")
            .WithExpiration(TimeSpan.FromMinutes(30))
            .WithTagGenerator((ctx, instance) => new[] { "product", $"product:{instance}" })
            .WithDescription("商品基础信息缓存")
            .Build();

        /// <summary>
        /// 商品列表（无占位符）
        /// </summary>
        public static readonly CacheKeyDefinition Product_List = CacheKeyDefinition
            .Create("product:list")
            .WithScope(CacheScope.Tenant)
            .WithExpiration(CacheExpirations.OneHour)
            .WithTagGenerator((ctx, _) => new[] { "product" })
            .WithDescription("商品列表缓存（租户隔离）")
            .Build();
        #endregion

        #region 商品库存缓存键
        /// <summary>
        /// 商品库存（单占位符：productId）
        /// </summary>
        public static readonly CacheKeyDefinition Stock_Product = CacheKeyDefinition
            .Create("stock:{productId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("productId")
            .WithExpiration(TimeSpan.FromSeconds(30))
            .WithTagGenerator((ctx, instance) => new[] { "stock", $"stock:{instance}" })
            .WithDescription("商品库存缓存（全局共享）")
            .Build();
        #endregion

        #region 购物车缓存键
        /// <summary>
        /// 用户购物车（单占位符：userId）
        /// </summary>
        public static readonly CacheKeyDefinition Cart_User = CacheKeyDefinition
            .Create("cart:{userId}")
            .WithScope(CacheScope.User)
            .WithInstanceKey("userId")
            .WithExpiration(TimeSpan.FromHours(24))
            .WithTagGenerator((ctx, instance) => new[] { "cart", $"user:{instance}" })
            .WithDescription("用户购物车缓存（用户隔离）")
            .Build();

        /// <summary>
        /// 购物车摘要（单占位符：userId）
        /// </summary>
        public static readonly CacheKeyDefinition Cart_Summary = CacheKeyDefinition
            .Create("cart:summary:{userId}")
            .WithScope(CacheScope.User)
            .WithInstanceKey("userId")
            .WithExpiration(TimeSpan.FromHours(24))
            .WithTagGenerator((ctx, instance) => new[] { "cart", $"user:{instance}" })
            .WithDescription("购物车摘要缓存（用户隔离）")
            .Build();
        #endregion

        #region 商品详情缓存键
        /// <summary>
        /// 商品详情页（多语言/多租户，单占位符：key=productId-lang-Country）
        /// </summary>
        public static readonly CacheKeyDefinition Product_Detail = CacheKeyDefinition
            .Create("product:detail:{key}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("key")
            .WithExpiration(TimeSpan.FromMinutes(10))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "product-detail",
                $"product:{instance.ToString()?.Split('-')[0] ?? string.Empty}"
            })
            .WithDescription("商品详情页缓存（多语言/多租户隔离，key格式：productId-lang-Country）")
            .Build();
        #endregion

        #region 促销活动缓存键
        /// <summary>
        /// 限时秒杀活动（单占位符：id）
        /// </summary>
        public static readonly CacheKeyDefinition Promotion_FlashSale = CacheKeyDefinition
            .Create("promotion:flash-sale:{id}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("id")
            .WithExpiration(TimeSpan.FromMinutes(10)) // 实际使用时可动态设置为活动持续时间
            .WithMaxRandomOffset(0)
            .WithTagGenerator((ctx, instance) => new[] { "promotion", "flash-sale" })
            .WithDescription("限时秒杀活动缓存（全局共享）")
            .Build();

        /// <summary>
        /// 优惠券批次（单占位符：batchId）
        /// </summary>
        public static readonly CacheKeyDefinition Coupon_Batch = CacheKeyDefinition
            .Create("coupon:batch:{batchId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("batchId")
            .WithExpiration(TimeSpan.FromHours(1))
            .WithTagGenerator((ctx, instance) => new[] { "coupon" })
            .WithDescription("优惠券批次缓存（全局共享）")
            .Build();
        #endregion

        #region 订单缓存键
        /// <summary>
        /// 订单信息（单占位符：orderId）
        /// </summary>
        public static readonly CacheKeyDefinition Order_Info = CacheKeyDefinition
            .Create("order:{orderId}")
            .WithScope(CacheScope.User)
            .WithInstanceKey("orderId")
            .WithExpiration(TimeSpan.FromMinutes(5))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "order",
                $"order:{instance}",
                $"user-order:{ctx.UserId ?? string.Empty}"
            })
            .WithDescription("订单信息缓存（用户隔离）")
            .Build();
        #endregion

        #region 商品分类与搜索缓存键
        /// <summary>
        /// 商品分类树（无占位符）
        /// </summary>
        public static readonly CacheKeyDefinition Category_Tree = CacheKeyDefinition
            .Create("category:tree")
            .WithScope(CacheScope.Global)
            .WithExpiration(TimeSpan.FromHours(24))
            .WithTagGenerator((ctx, _) => new[] { "category" })
            .WithDescription("商品分类树缓存（全局共享，长时间有效）")
            .Build();

        /// <summary>
        /// 搜索结果（单占位符：key=keyword:page）
        /// </summary>
        public static readonly CacheKeyDefinition Search_Result = CacheKeyDefinition
            .Create("search:{key}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("key")
            .WithExpiration(TimeSpan.FromMinutes(5))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "search",
                $"search:{instance.ToString()?.Split(':')[0] ?? string.Empty}"
            })
            .WithDescription("商品搜索结果缓存（租户隔离，key格式：keyword:page）")
            .Build();
        #endregion

        #region 个性化推荐缓存键
        /// <summary>
        /// 首页个性化推荐（单占位符：userId）
        /// </summary>
        public static readonly CacheKeyDefinition Recommend_Home = CacheKeyDefinition
            .Create("recommend:home:{userId}")
            .WithScope(CacheScope.User)
            .WithInstanceKey("userId")
            .WithExpiration(TimeSpan.FromHours(1))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "recommend",
                $"user-recommend:{instance}"
            })
            .WithDescription("首页个性化推荐缓存（用户隔离）")
            .Build();
        #endregion

        #region 秒杀活动缓存键
        /// <summary>
        /// 秒杀活动详情（单占位符：activityId）
        /// </summary>
        public static readonly CacheKeyDefinition Seckill_Activity = CacheKeyDefinition
            .Create("seckill:{activityId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("activityId")
            .WithExpiration(TimeSpan.FromMinutes(10))
            .WithTagGenerator((ctx, instance) => new[] { "seckill", $"activity:{instance}" })
            .WithDescription("秒杀活动详情缓存（全局共享，防止超卖）")
            .Build();
        #endregion

        #region 商家店铺缓存键
        /// <summary>
        /// 店铺信息（单占位符：shopId）
        /// </summary>
        public static readonly CacheKeyDefinition Shop_Info = CacheKeyDefinition
            .Create("shop:info:{shopId}")
            .WithScope(CacheScope.Tenant)
            .WithInstanceKey("shopId")
            .WithExpiration(TimeSpan.FromHours(12))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "shop",
                $"shop:{instance}",
                $"tenant-shop:{ctx.TenantId ?? string.Empty}"
            })
            .WithDescription("店铺信息缓存（租户隔离，多商家平台支持）")
            .Build();
        #endregion

        #region 评论评分缓存键
        /// <summary>
        /// 商品评论统计（单占位符：productId）
        /// </summary>
        public static readonly CacheKeyDefinition Review_Stats = CacheKeyDefinition
            .Create("review:stats:{productId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("productId")
            .WithExpiration(TimeSpan.FromMinutes(30))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "review-stats",
                $"product:{instance}"
            })
            .WithDescription("商品评论统计缓存（全局共享）")
            .Build();
        #endregion

        #region 物流信息缓存键
        /// <summary>
        /// 物流跟踪信息（单占位符：trackingNumber）
        /// </summary>
        public static readonly CacheKeyDefinition Logistics_Tracking = CacheKeyDefinition
            .Create("logistics:{trackingNumber}")
            .WithScope(CacheScope.User)
            .WithInstanceKey("trackingNumber")
            .WithExpiration(TimeSpan.FromMinutes(3))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "logistics",
                $"tracking:{instance}"
            })
            .WithDescription("物流跟踪信息缓存（用户隔离，短时有效）")
            .Build();
        #endregion

        #region 首页Banner缓存键
        /// <summary>
        /// 首页Banner（单占位符：lang）
        /// </summary>
        public static readonly CacheKeyDefinition Banner_Homepage = CacheKeyDefinition
            .Create("banner:homepage:{lang}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("lang")
            .WithExpiration(TimeSpan.FromHours(6))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "banner",
                $"banner-lang:{instance}"
            })
            .WithDescription("首页Banner缓存（全局共享，多语言支持）")
            .Build();
        #endregion

        #region 会员等级缓存键
        /// <summary>
        /// 会员等级权益（单占位符：userId）
        /// </summary>
        public static readonly CacheKeyDefinition Membership_User = CacheKeyDefinition
            .Create("membership:{userId}")
            .WithScope(CacheScope.User)
            .WithInstanceKey("userId")
            .WithExpiration(TimeSpan.FromHours(24))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "membership",
                $"user-membership:{instance}"
            })
            .WithDescription("会员等级权益缓存（用户隔离）")
            .Build();
        #endregion

        #region 热销排行榜缓存键
        /// <summary>
        /// 热销排行榜（单占位符：categoryId）
        /// </summary>
        public static readonly CacheKeyDefinition Ranking_HotSale = CacheKeyDefinition
            .Create("ranking:hot-sale:{categoryId}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("categoryId")
            .WithExpiration(TimeSpan.FromMinutes(10))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "ranking",
                "hot-sale",
                $"category:{instance}"
            })
            .WithDescription("热销排行榜缓存（全局共享，分类维度）")
            .Build();
        #endregion

        #region 配送区域缓存键
        /// <summary>
        /// 配送区域信息（单占位符：key=province-city）
        /// </summary>
        public static readonly CacheKeyDefinition Shipping_Region = CacheKeyDefinition
            .Create("shipping:region:{key}")
            .WithScope(CacheScope.Global)
            .WithInstanceKey("key")
            .WithExpiration(TimeSpan.FromDays(1))
            .WithTagGenerator((ctx, instance) => new[]
            {
                "shipping",
                $"province:{instance.ToString()?.Split('-')[0] ?? string.Empty}"
            })
            .WithDescription("配送区域信息缓存（全局共享，key格式：province-city）")
            .Build();
        #endregion

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
}

