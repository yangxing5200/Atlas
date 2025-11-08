using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common.Extensions;
using Atlas.Data.Tests.TestEntities;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Reflection.Emit;

namespace Atlas.Data.Tests
{
    /// <summary>
    /// 测试用数据库上下文
    /// </summary>
    public class TestDbContext : DbContext, IHasCurrentUser
    {
        private readonly ICurrentUserService _currentUserService;

        public TestDbContext(
            DbContextOptions<TestDbContext> options,
            ICurrentUserService currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        // ⭐ 实现IHasCurrentUser接口
        public long? CurrentUserId => _currentUserService?.UserId;
        public long? CurrentTenantId => _currentUserService?.TenantId;

        // DbSet定义
        public DbSet<TestTenant> TestTenants { get; set; }
        public DbSet<TestUser> TestUsers { get; set; }
        public DbSet<TestProduct> TestProducts { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 1. 应用所有实体配置
            modelBuilder.ApplyConfigurationsFromAssembly(
                typeof(TestDbContext).Assembly);

            // 2. 移除所有外键约束 
            modelBuilder.RemoveAllForeignKeyConstraints();

            // 3. 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 4. 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();
            // TestTenant配置
            modelBuilder.Entity<TestTenant>(entity =>
            {
                entity.ToTable("test_tenants");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd(); // 自增ID
                entity.Property(e => e.Name).IsRequired().HasMaxLength(100);
                entity.Property(e => e.Code).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Status).HasDefaultValue(1);
                entity.Property(e => e.Remark).HasMaxLength(500);
                entity.Property(e => e.Version).HasDefaultValue(0);
            });

            // TestUser配置
            modelBuilder.Entity<TestUser>(entity =>
            {
                entity.ToTable("test_users");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedNever(); // Snowflake ID，应用层生成
                entity.Property(e => e.UserName).IsRequired().HasMaxLength(50);
                entity.Property(e => e.Email).HasMaxLength(100);
                entity.Property(e => e.Phone).HasMaxLength(20);
                entity.Property(e => e.Version).HasDefaultValue(0);
            });

            // TestProduct配置（无审计字段）
            modelBuilder.Entity<TestProduct>(entity =>
            {
                entity.ToTable("test_products");
                entity.HasKey(e => e.Id);
                entity.Property(e => e.Id).ValueGeneratedOnAdd();
                entity.Property(e => e.ProductName).IsRequired().HasMaxLength(200);
                entity.Property(e => e.Price).HasColumnType("decimal(18,2)");
                entity.Property(e => e.Stock).HasDefaultValue(0);
            });
        }
    }
}