using Atlas.Core.Context;
using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common.Extensions;
using Atlas.Models.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Internal;
using System.Reflection;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// Tenant数据库上下文
    /// </summary>
    public class AtlasTenantDbContext : DbContext, IHasCurrentUser
    {
        private readonly ICurrentIdentity _currentUserService;
        private readonly ITenantContext _tenantContext;
        private readonly string? _connectionString;

        public AtlasTenantDbContext(
            DbContextOptions<AtlasTenantDbContext> options,
            ITenantContext tenantContext)
            : base(options)
        {
            _tenantContext = tenantContext;
            _connectionString = tenantContext.TenantConnectionString;
        }

        public AtlasTenantDbContext(
            DbContextOptions<AtlasTenantDbContext> options,
            ICurrentIdentity currentUserService)
            : base(options)
        {
            _currentUserService = currentUserService;
        }

        // 实现IHasCurrentUser接口
        public long? CurrentUserId => _currentUserService?.UserId;
        public long? StoreId => _currentUserService?.StoreId;
        public long? CurrentTenantId => _currentUserService?.TenantId;
        internal DbSet<TEntity> GetDbSet<TEntity>() where TEntity : class
        {
            // Debug模式下验证调用者
            DbContextAccessValidator.ValidateAccess();
            return Set<TEntity>();
        }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!string.IsNullOrEmpty(_connectionString))
            {
                optionsBuilder.UseMySql(
                    _connectionString,
                    ServerVersion.AutoDetect(_connectionString),
                    mysqlOptions =>
                    {
                        // 启用重试机制
                        mysqlOptions.EnableRetryOnFailure(
                            maxRetryCount: 3,
                            maxRetryDelay: TimeSpan.FromSeconds(5),
                            errorNumbersToAdd: null);

                        // 命令超时
                        mysqlOptions.CommandTimeout(30);
                    });

                // 开发环境启用敏感数据记录
                if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
                {
                    optionsBuilder.EnableSensitiveDataLogging();
                }
            }
        }
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // 1. 应用所有实体配置
            var migrationsAssembly = Assembly.Load("Atlas.Data.Tenant.Migrations");
            modelBuilder.ApplyConfigurationsFromAssembly(migrationsAssembly);
            ConfigureIdGenerationStrategy(modelBuilder);
            // 2. 移除所有外键约束 
            modelBuilder.RemoveAllForeignKeyConstraints();

            // 3. 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 4. 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();
        }

        /// <summary>
        /// 自动配置ID生成策略
        /// - 实现了 ISnowflakeId 接口的实体：不使用数据库生成（由应用层生成雪花ID）
        /// - 其他实体：使用数据库自增ID
        /// </summary>
        private void ConfigureIdGenerationStrategy(ModelBuilder modelBuilder)
        {
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                var clrType = entityType.ClrType;

                // 跳过非实体类型
                if (!typeof(IBaseEntity).IsAssignableFrom(clrType))
                {
                    continue;
                }

                var idProperty = entityType.FindProperty(nameof(IBaseEntity.Id));
                if (idProperty == null)
                {
                    continue;
                }

                // 检查是否实现了 ISnowflakeId 接口
                if (typeof(ISnowflakeId).IsAssignableFrom(clrType))
                {
                    // 雪花ID：不使用数据库生成
                    modelBuilder.Entity(clrType)
                        .Property(nameof(IBaseEntity.Id))
                        .ValueGeneratedNever();
                }
                else
                {
                    // 默认：使用数据库自增ID
                    modelBuilder.Entity(clrType)
                        .Property(nameof(IBaseEntity.Id))
                        .ValueGeneratedOnAdd();
                }
            }
        }
    }
}