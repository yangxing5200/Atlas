using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common.Extensions;
using Atlas.Models.Tenant;
using Microsoft.EntityFrameworkCore;
using System.Reflection;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// Tenant数据库上下文
    /// </summary>
    public class AtlasTenantDbContext : DbContext, IHasCurrentUser
    {
        private readonly ICurrentUserService _currentUserService;

        public AtlasTenantDbContext(
            DbContextOptions<AtlasTenantDbContext> options,
            ICurrentUserService currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        // 实现IHasCurrentUser接口
        public long? CurrentUserId => _currentUserService.UserId;
        public long? CurrentTenantId => _currentUserService.TenantId;

        // DbSet定义
        public DbSet<Store> Stores { get; set; }
        // public DbSet<Order> Orders { get; set; }
        // public DbSet<Product> Products { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. 应用所有实体配置
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(AtlasTenantDbContext).Assembly);

            // 2. 移除所有外键约束 
            modelBuilder.RemoveAllForeignKeyConstraints();

            // 3. 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 4. 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();

            // 5. 租户过滤器（关键修复：通过实例方法构建，访问实例的_currentUserService）
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                if (typeof(ITenantEntity).IsAssignableFrom(entityType.ClrType))
                {
                    // 调用实例方法，而非静态方法
                    var method = GetType().GetMethod(nameof(SetTenantFilter),
                        BindingFlags.NonPublic | BindingFlags.Instance)!
                        .MakeGenericMethod(entityType.ClrType);
                    method.Invoke(this, new object[] { modelBuilder });
                }
            }
        }

        /// <summary>
        /// 实例方法：设置租户查询过滤器
        /// </summary>
        private void SetTenantFilter<TEntity>(ModelBuilder modelBuilder)
            where TEntity : class, ITenantEntity
        {
            modelBuilder.Entity<TEntity>().HasQueryFilter(e =>
                // 注意：如果CurrentTenantId可能为null，需处理空值（避免查询过滤器失效）
                _currentUserService.TenantId == null || e.TenantId == _currentUserService.TenantId);
        }
    }
}