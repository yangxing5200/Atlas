using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Repositories;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Integration.Tests.Infrastructure;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Integration.Tests
{
    /// <summary>
    /// 租户数据库集成测试基类
    /// 配置完整的 DI 容器，支持多租户数据库访问
    /// </summary>
    public abstract class TenantTestBase : IntegrationTestBase
    {
        protected FakeCurrentIdentity FakeIdentity { get; private set; } = null!;

        /// <summary>
        /// 测试租户配置
        /// </summary>
        protected static class TestTenants
        {
            public const long DemoCompany = 1;          // 演示公司
            public const long ChainEnterprise = 2;      // 测试连锁企业
            public const long PersonalTenant = 3;       // 个人版租户
        }

        /// <summary>
        /// 测试用户ID
        /// </summary>
        protected static class TestUsers
        {
            public const long AdminUser = 1001;
            public const long NormalUser = 1002;
            public const long TestUser = 1003;
        }

        protected override void ConfigureServices(
            IServiceCollection services,
            IConfiguration configuration)
        {
            // FakeCurrentIdentity（单例，便于测试中切换）
            services.AddSingleton<ICurrentIdentity>(sp =>
            {
                var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
                var lazyCache = new Lazy<ICacheService>(() =>
                    sp.GetRequiredService<ICacheService>());
                var lazyStoreRepository = new Lazy<IStoreRepository>(() =>
                    sp.GetRequiredService<IStoreRepository>());

                return new FakeCurrentIdentity(lazyStoreRepository, lazyCache);
            });

            ConfigureAdditionalServices(services, configuration);
        }

        /// <summary>
        /// 子类可重写以添加额外服务
        /// </summary>
        protected virtual void ConfigureAdditionalServices(
            IServiceCollection services,
            IConfiguration configuration)
        {
        }

        protected override async Task OnInitializeAsync()
        {
            // 验证全局数据库连接
            var globalContext = GetService<AtlasGlobalDbContext>();
            var canConnect = await globalContext.Database.CanConnectAsync();

            if (!canConnect)
            {
                throw new InvalidOperationException(
                    "无法连接到全局数据库，请检查连接字符串配置");
            }
            await base.OnInitializeAsync();
            FakeIdentity = (FakeCurrentIdentity)GetService<ICurrentIdentity>();
            SwitchToTenant(TestTenants.ChainEnterprise, TestUsers.TestUser);
            var factory = ServiceProvider.GetService<ITenantDbContextFactory>();
            await factory.GetReadonlyDbContextAsync();
          

        }
        protected void SwitchToTenant(long tenantId, long userId = TestUsers.AdminUser, long? storeId = null)
        {
            FakeIdentity.SetIdentity(tenantId, userId, storeId);
        }
        /// <summary>
        /// 直接获取租户数据库上下文（用于验证数据）
        /// </summary>
        protected async Task<AtlasTenantDbContext> GetTenantDbContextAsync()
        {
            var factory = GetService<ITenantDbContextFactory>();
            return await factory.GetMasterDbContextAsync();
        }

        /// <summary>
        /// 验证数据库连接是否指向预期的租户
        /// </summary>
        protected async Task<string> GetCurrentDatabaseNameAsync()
        {
            var context = await GetTenantDbContextAsync();
            var connection = context.Database.GetDbConnection();
            await connection.OpenAsync();
            var dbName = connection.Database;
            await connection.CloseAsync();
            return dbName;
        }

    }
}
