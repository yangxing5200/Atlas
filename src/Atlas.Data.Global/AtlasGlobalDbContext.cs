using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common.Extensions;
using Atlas.Models.Global;

using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Global
{
    /// <summary>
    /// Global数据库上下文
    /// </summary>
    public class AtlasGlobalDbContext : DbContext, IHasCurrentUser
    {
        private readonly ICurrentUserService _currentUserService;

        public AtlasGlobalDbContext(
            DbContextOptions<AtlasGlobalDbContext> options,
            ICurrentUserService currentUserService)
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
        // public DbSet<User> Users { get; set; }
        // public DbSet<Setting> Settings { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. 应用所有实体配置
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(AtlasGlobalDbContext).Assembly);

            // 2. 移除所有外键约束 
            modelBuilder.RemoveAllForeignKeyConstraints();

            // 3. 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 4. 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();
        }
    }
}