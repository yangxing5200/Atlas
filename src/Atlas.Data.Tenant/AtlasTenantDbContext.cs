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

            // 2. 移除所有外键约束 
            modelBuilder.RemoveAllForeignKeyConstraints();

            // 3. 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 4. 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();
        }
    }
}