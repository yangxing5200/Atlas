using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Infrastructure.Caching.Tests.Helpers
{
    /// <summary>
    /// 电商测试场景专用缓存清理工具
    /// </summary>
    public class EcommerceCacheCleanupHelper
    {
        private readonly ICacheService _cacheService;
        private readonly IScopeContextAccessor _scopeAccessor;
        private readonly IConfiguration _configuration;

        public EcommerceCacheCleanupHelper(
            ICacheService cacheService,
            IScopeContextAccessor scopeAccessor,
            IConfiguration configuration)
        {
            _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
            _scopeAccessor = scopeAccessor ?? throw new ArgumentNullException(nameof(scopeAccessor));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        /// <summary>
        /// 清理电商测试中的所有缓存数据
        /// </summary>
        public async Task CleanupAllEcommerceCachesAsync()
        {
            // 设置默认作用域
            _scopeAccessor.Current = new ScopeContext { TenantId = "cleanup-tenant", UserId = "cleanup-user" };

            // 按模块清理缓存
            await CleanupProductCachesAsync();
            await CleanupCartCachesAsync();
            await CleanupOrderCachesAsync();
            await CleanupPromotionCachesAsync();
            await CleanupUserCachesAsync();
            await CleanupSearchCachesAsync();
            await CleanupSystemCachesAsync();
            
            // 最后清理所有残留
            await _cacheService.ClearAsync();
        }

        /// <summary>
        /// 清理商品相关缓存
        /// </summary>
        private async Task CleanupProductCachesAsync()
        {
            var productTags = new[]
            {
                "product", "product-detail", "stock", "category", "review-stats"
            };

            foreach (var tag in productTags)
            {
                await _cacheService.InvalidateByTagAsync(tag);
            }
        }

        /// <summary>
        /// 清理购物车相关缓存
        /// </summary>
        private async Task CleanupCartCachesAsync()
        {
            await _cacheService.InvalidateByTagAsync("cart");
            
            // 清理用户特定的购物车缓存
            var testUsers = new[] { "user-001", "user-002", "user-456" };
            foreach (var userId in testUsers)
            {
                await _cacheService.InvalidateByTagAsync($"user:{userId}");
            }
        }

        /// <summary>
        /// 清理订单相关缓存
        /// </summary>
        private async Task CleanupOrderCachesAsync()
        {
            await _cacheService.InvalidateByTagAsync("order");
            
            // 清理测试订单缓存
            var testOrders = new[] { "ORD-001", "ORD-002", "ORD-003", "ORD-20240101-001" };
            foreach (var orderId in testOrders)
            {
                await _cacheService.InvalidateByTagAsync($"order:{orderId}");
                await _cacheService.InvalidateByTagAsync($"user-order:{orderId}");
            }
        }

        /// <summary>
        /// 清理促销活动缓存
        /// </summary>
        private async Task CleanupPromotionCachesAsync()
        {
            var promotionTags = new[]
            {
                "promotion", "flash-sale", "coupon"
            };

            foreach (var tag in promotionTags)
            {
                await _cacheService.InvalidateByTagAsync(tag);
            }
        }

        /// <summary>
        /// 清理用户相关缓存
        /// </summary>
        private async Task CleanupUserCachesAsync()
        {
            await _cacheService.InvalidateByTagAsync("membership");
            await _cacheService.InvalidateByTagAsync("recommend");
            
            // 清理所有用户推荐缓存
            var testUsers = new[] { "user-001", "user-002", "user-vip-001", "user-789", "user-123" };
            foreach (var userId in testUsers)
            {
                await _cacheService.InvalidateByTagAsync($"user-recommend:{userId}");
                await _cacheService.InvalidateByTagAsync($"user-membership:{userId}");
            }
        }

        /// <summary>
        /// 清理搜索相关缓存
        /// </summary>
        private async Task CleanupSearchCachesAsync()
        {
            await _cacheService.InvalidateByTagAsync("search");
            
            // 清理测试搜索关键词缓存
            var testKeywords = new[] { "手机", "电子产品", "服装" };
            foreach (var keyword in testKeywords)
            {
                await _cacheService.InvalidateByTagAsync($"search:{keyword}");
            }
        }

        /// <summary>
        /// 清理系统级缓存
        /// </summary>
        private async Task CleanupSystemCachesAsync()
        {
            var systemTags = new[]
            {
                "seckill", "shop", "banner", "logistics", "ranking", "shipping"
            };

            foreach (var tag in systemTags)
            {
                await _cacheService.InvalidateByTagAsync(tag);
            }
        }

        /// <summary>
        /// 生成测试清理报告
        /// </summary>
        public async Task<CacheCleanupReport> GenerateCleanupReportAsync()
        {
            var report = new CacheCleanupReport
            {
                CleanupTime = DateTime.UtcNow,
                CacheProvider = _configuration["CacheSettings:Provider"] ?? "Unknown"
            };

            try
            {
                var stats = await _cacheService.GetStatisticsAsync();
                if (stats != null)
                {
                    report.FinalStats = stats;
                }
            }
            catch (Exception ex)
            {
                report.ErrorMessage = ex.Message;
            }

            return report;
        }
    }

    /// <summary>
    /// 缓存清理报告
    /// </summary>
    public class CacheCleanupReport
    {
        public DateTime CleanupTime { get; set; }
        public string CacheProvider { get; set; } = string.Empty;
        public object? FinalStats { get; set; }
        public int TotalOperations { get; set; }
        public string? ErrorMessage { get; set; }

        public override string ToString()
        {
            return $@"
缓存清理报告:
- 清理时间: {CleanupTime:yyyy-MM-dd HH:mm:ss}
- 缓存提供器: {CacheProvider}
- 总操作数: {TotalOperations}
- 状态: {(string.IsNullOrEmpty(ErrorMessage) ? "成功" : $"失败 - {ErrorMessage}")}";
        }
    }
}