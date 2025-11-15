// Atlas.Integration.Tests/GlobalDatabase/GlobalDatabaseTestBase.cs
using Atlas.Core.Context;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Data.Global.Seeds;
using Atlas.Integration.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

namespace Atlas.Integration.Tests.GlobalDatabase
{
    /// <summary>
    /// Global 数据库测试基类
    /// </summary>
    public abstract class GlobalDatabaseTestBase : IntegrationTestBase
    {
        protected override void ConfigureServices(IServiceCollection services, IConfiguration configuration)
        {
            // 注册 Mock CurrentIdentity
            services.AddSingleton<ICurrentIdentity>(new MockCurrentIdentity
            {
                UserId = 1,
                UserName = "TestUser",
                TenantId = 1,
                StoreId = 1,
                IsAuthenticated = true
            });

            // 注册 Global DbContext
            var connectionString = configuration.GetConnectionString("AtlasGlobal")!;
            services.AddDbContext<AtlasGlobalDbContext>(options =>
            {
                options.UseMySql(
                    connectionString,
                    ServerVersion.AutoDetect(connectionString),
                    mySqlOptions =>
                    {
                        mySqlOptions.EnableRetryOnFailure(3);
                    });
                options.EnableSensitiveDataLogging();
                options.EnableDetailedErrors();
            });

            // 注册日志
            services.AddLogging(builder =>
            {
                builder.AddConsole();
                builder.SetMinimumLevel(LogLevel.Information);
            });
        }

        protected override async Task OnInitializeAsync()
        {
            await EnsureDatabaseAsync();
            await SeedDatabaseAsync();
        }

        protected override async Task OnDisposeAsync()
        {
            // 可选：测试后清理
            // await CleanupDatabaseAsync();
        }

        /// <summary>
        /// 确保数据库存在并应用迁移
        /// </summary>
        private async Task EnsureDatabaseAsync()
        {
            var context = GetService<AtlasGlobalDbContext>();
            await context.Database.EnsureDeletedAsync();
            await context.Database.EnsureCreatedAsync();
        }

        /// <summary>
        /// 执行 Global 数据库种子数据
        /// </summary>
        private async Task SeedDatabaseAsync()
        {
            var context = GetService<AtlasGlobalDbContext>();
            var logger = GetOptionalService<ILogger<GlobalDataSeeder>>();

            var seeder = new GlobalDataSeeder(context, logger);
            await seeder.SeedAsync();
        }
    }

    /// <summary>
    /// Mock CurrentIdentity（测试用）
    /// </summary>
    public class MockCurrentIdentity : ICurrentIdentity
    {
        public long? UserId { get; set; }
        public string UserName { get; set; } = string.Empty;
        public long? StoreId { get; set; }
        public long? TenantId { get; set; }
        public bool IsAuthenticated { get; set; }

        public Task<List<long>> GetAccessibleStoreIdsAsync(CancellationToken ct = default)
        {
            return Task.FromResult(StoreId.HasValue
                ? new List<long> { StoreId.Value }
                : new List<long>());
        }
    }
}