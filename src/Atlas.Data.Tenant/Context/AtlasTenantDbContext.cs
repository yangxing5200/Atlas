using Atlas.Core.Context;
using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common.Extensions;
using Atlas.Models.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Internal;
using System.Reflection;

namespace Atlas.Data.Tenant.Context
{
    /// <summary>
    /// Tenant数据库上下文
    /// </summary>
    public class AtlasTenantDbContext : DbContext, IHasCurrentUser
    {
        private readonly ICurrentIdentity _currentUserService;
        private readonly string? _connectionString;
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 应用所有实体配置
            var migrationsAssembly = Assembly.Load("Atlas.Data.Tenant.Migrations");
            modelBuilder.ApplyConfigurationsFromAssembly(migrationsAssembly);

            // 配置ID生成策略
            modelBuilder.ConfigureIdGenerationStrategy();
       
            // 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();

            // 移除所有外键约束 
            modelBuilder.RemoveAllForeignKeyConstraints();
        }
    }
}