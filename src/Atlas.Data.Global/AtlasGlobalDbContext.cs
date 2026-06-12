using Atlas.Core.Entities.Global;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common;
using Atlas.Data.Common.Extensions;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Atlas.Data.Global
{
    /// <summary>
    /// Global数据库上下文
    /// </summary>
    public class AtlasGlobalDbContext : DbContextBase, IHasCurrentUser
    {
        private readonly ICurrentIdentity _currentUserService;

        public AtlasGlobalDbContext(
            DbContextOptions<AtlasGlobalDbContext> options,
            ICurrentIdentity currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        //   实现IHasCurrentUser接口（供SmartBatchExtensions使用）
        public long? CurrentUserId => _currentUserService.UserId;
        public long? StoreId => _currentUserService.StoreId;
        public long? CurrentTenantId => _currentUserService.TenantId;

        // DbSet定义
        public DbSet<Tenant> Tenants { get; set; }
        public DbSet<DatabaseInstance> DatabaseInstances { get; set; }
        public DbSet<DatabaseMasterServer> DatabaseMasterServers { get; set; }
        public DbSet<DatabaseReadonlyServer> DatabaseReadonlyServers { get; set; }
        public DbSet<DatabaseServerConfig> DatabaseServerConfigs { get; set; }
        public DbSet<BackgroundJob> BackgroundJobs { get; set; }
        public DbSet<BackgroundWorkerHeartbeat> BackgroundWorkerHeartbeats { get; set; }
        public DbSet<ExportJob> ExportJobs { get; set; }
        public DbSet<TenantSchemaMigrationState> TenantSchemaMigrationStates { get; set; }
        public DbSet<Capability> Capabilities { get; set; }
        public DbSet<FeaturePackage> FeaturePackages { get; set; }
        public DbSet<PackageCapability> PackageCapabilities { get; set; }
        public DbSet<TenantEntitlement> TenantEntitlements { get; set; }
        public DbSet<MenuItem> MenuItems { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. 应用所有实体配置
            var migrationsAssembly = Assembly.Load("Atlas.Data.Global.Migrations");
            modelBuilder.ApplyConfigurationsFromAssembly(migrationsAssembly);

            // 2. MassTransit transactional outbox tables
            modelBuilder.AddInboxStateEntity();
            modelBuilder.AddOutboxMessageEntity();
            modelBuilder.AddOutboxStateEntity();
            ConfigureMassTransitOutbox(modelBuilder);

            // 3. 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 4. 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();

            // 5. 移除所有外键约束
            modelBuilder.RemoveAllForeignKeyConstraints();
        }

        private static void ConfigureMassTransitOutbox(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity("MassTransit.EntityFrameworkCoreIntegration.OutboxMessage", builder =>
            {
                builder.Property<string>("Body").HasColumnType("longtext");
                builder.Property<string>("Headers").HasColumnType("longtext");
                builder.Property<string>("MessageType").HasColumnType("longtext");
                builder.Property<string>("Properties").HasColumnType("longtext");
            });
        }
    }
}
