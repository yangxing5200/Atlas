using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common.Extensions;
using Atlas.Models.Global.Entities;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Atlas.Data.Global
{
    /// <summary>
    /// Global数据库上下文
    /// </summary>
    public class AtlasGlobalDbContext : DbContext, IHasCurrentUser
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. 应用所有实体配置
            var migrationsAssembly = Assembly.Load("Atlas.Data.Global.Migrations");
            modelBuilder.ApplyConfigurationsFromAssembly(migrationsAssembly);

            // 2. 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 3. 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();

            // 4. 移除所有外键约束
            modelBuilder.RemoveAllForeignKeyConstraints();
        }
    }
}