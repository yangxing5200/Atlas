using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Providers.Hybrid;
using Atlas.Infrastructure.Caching.Scoping;
using Atlas.Infrastructure.Caching.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Integration
{
    /// <summary>
    /// 优化的集成测试基类 - 包含完整的测试后缓存清理功能
    /// </summary>
    public class OptimizedCacheSystemIntegrationTests : IAsyncLifetime
    {
        protected IServiceProvider ServiceProvider { get; private set; } = null!;
        protected IServiceScope Scope { get; private set; } = null!;
        protected IConfiguration Configuration { get; private set; } = null!;

        protected ICacheService _cacheService = null!;
        protected IScopeContextAccessor _scopeAccessor = null!;
        protected EcommerceCacheCleanupHelper? _cleanupHelper;

        // 配置选项
        protected virtual bool EnableCacheCleanup => true;
        protected virtual bool GenerateCleanupReport => true;
        protected virtual bool LogStatistics => true;

        public virtual async Task InitializeAsync()
        {
            // 构建配置
            Configuration = BuildConfiguration();

            var services = new ServiceCollection();

            // 将配置注入到服务容器
            services.AddSingleton(Configuration);

            ConfigureServices(services);
            ServiceProvider = services.BuildServiceProvider();
            Scope = ServiceProvider.CreateScope();

            // 从服务容器中获取服务
            _cacheService = Scope.ServiceProvider.GetRequiredService<ICacheService>();
            _scopeAccessor = Scope.ServiceProvider.GetRequiredService<IScopeContextAccessor>();

            // 初始化清理助手
            if (EnableCacheCleanup)
            {
                _cleanupHelper = new EcommerceCacheCleanupHelper(_cacheService, _scopeAccessor, Configuration);
            }

            await OnInitializeAsync();
        }

        protected virtual IConfiguration BuildConfiguration()
        {
            var configBuilder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: true, reloadOnChange: false)
                .AddJsonFile("appsettings.Test.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables();

            return configBuilder.Build();
        }

        protected virtual void ConfigureServices(IServiceCollection services)
        {
            // 从配置读取缓存提供器类型
            var cacheProvider = Configuration["CacheSettings:Provider"] ?? "Memory";

            // 使用扩展方法配置缓存服务
            services.AddAtlasCaching();

            // 根据配置选择缓存提供器
            switch (cacheProvider.ToLower())
            {
                case "redis":
                    ConfigureRedisCache(services);
                    break;

                case "hybrid":
                    ConfigureHybridCache(services);
                    break;

                case "memory":
                default:
                    services.AddMemoryCaching();
                    break;
            }
        }

        private void ConfigureRedisCache(IServiceCollection services)
        {
            var connectionString = Configuration["CacheSettings:Redis:ConnectionString"];
            var instanceName = Configuration["CacheSettings:Redis:InstanceName"];

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Redis connection string is not configured in appsettings.json");
            }

            services.AddRedisCaching(connectionString, instanceName);
        }

        private void ConfigureHybridCache(IServiceCollection services)
        {
            var connectionString = Configuration["CacheSettings:Hybrid:RedisConnectionString"];

            if (string.IsNullOrEmpty(connectionString))
            {
                throw new InvalidOperationException(
                    "Hybrid cache Redis connection string is not configured in appsettings.json");
            }

            services.AddHybridCaching(connectionString, options =>
            {
                // 从配置读取 Hybrid 缓存选项
                var l1ExpirationMinutes = Configuration.GetValue<int?>(
                    "CacheSettings:Hybrid:L1ExpirationMinutes");

                if (l1ExpirationMinutes.HasValue)
                {
                    options.L1Expiration = TimeSpan.FromMinutes(l1ExpirationMinutes.Value);
                }
            });
        }

        protected virtual Task OnInitializeAsync()
        {
            return Task.CompletedTask;
        }

        public virtual async Task DisposeAsync()
        {
            try
            {
                // 执行测试后清理
                if (EnableCacheCleanup && _cleanupHelper != null)
                {
                    await PerformTestCleanupAsync();
                }

                // 记录最终统计信息
                if (LogStatistics && _cacheService != null)
                {
                    await LogFinalStatisticsAsync();
                }

                // 生成清理报告
                if (GenerateCleanupReport && _cleanupHelper != null)
                {
                    var report = await _cleanupHelper.GenerateCleanupReportAsync();
                    Console.WriteLine(report.ToString());
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Warning: Test cleanup failed: {ex.Message}");
            }
            finally
            {
                // 清理作用域和释放资源
                Scope?.Dispose();
                if (ServiceProvider is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            await Task.CompletedTask;
        }

        /// <summary>
        /// 执行测试后清理
        /// </summary>
        protected virtual async Task PerformTestCleanupAsync()
        {
            if (_cleanupHelper == null) return;

            try
            {
                await _cleanupHelper.CleanupAllEcommerceCachesAsync();
            }
            catch (Exception ex)
            {
                // 如果电商清理失败，尝试基本清理
                Console.WriteLine($"Ecommerce cleanup failed, trying basic cleanup: {ex.Message}");
                await BasicCacheCleanupAsync();
            }
        }

        /// <summary>
        /// 基本缓存清理（备用方案）
        /// </summary>
        protected virtual async Task BasicCacheCleanupAsync()
        {
            try
            {
                // 1. 清空所有缓存
                await _cacheService.ClearAsync();

                // 2. 清理测试标签
                var testTags = new[] 
                { 
                    "test", "product", "cart", "order", "promotion", "search",
                    "recommend", "seckill", "shop", "review", "banner", "membership"
                };

                foreach (var tag in testTags)
                {
                    try
                    {
                        await _cacheService.InvalidateByTagAsync(tag);
                    }
                    catch
                    {
                        // 忽略单个标签清理失败
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Basic cleanup failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 记录最终统计信息
        /// </summary>
        protected virtual async Task LogFinalStatisticsAsync()
        {
            try
            {
                var stats = await _cacheService.GetStatisticsAsync();
                if (stats != null)
                {
                    Console.WriteLine($@"
测试完成统计报告:
==================
缓存操作统计:
- 总设置次数: {stats.TotalSets:N0}
- 总获取次数: {stats.TotalGets:N0}
- 命中次数: {stats.TotalHits:N0}
- 未命中次数: {stats.TotalMisses:N0}
- 命中率: {stats.HitRate:P2}
- 总无效化次数: {stats.TotalInvalidations:N0}

测试完成时间: {DateTime.UtcNow:yyyy-MM-dd HH:mm:ss}");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to get final statistics: {ex.Message}");
            }
        }

        #region 辅助方法

        /// <summary>
        /// 设置测试作用域上下文
        /// </summary>
        protected void SetTestScopeContext(string tenantId = "test-tenant", string userId = "test-user")
        {
            _scopeAccessor.Current = new ScopeContext 
            { 
                TenantId = tenantId, 
                UserId = userId 
            };
        }

        /// <summary>
        /// 获取缓存统计信息（用于测试中的调试）
        /// </summary>
        protected async Task<CacheStatistics?> GetCurrentStatisticsAsync()
        {
            try
            {
                return await _cacheService.GetStatisticsAsync();
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 强制清理特定测试数据
        /// </summary>
        protected async Task CleanupSpecificTestDataAsync(string[] tags)
        {
            foreach (var tag in tags)
            {
                await _cacheService.InvalidateByTagAsync(tag);
            }
        }

        #endregion
    }
}