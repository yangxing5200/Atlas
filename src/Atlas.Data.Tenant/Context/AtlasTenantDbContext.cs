using Atlas.Core.Context;
using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common;
using Atlas.Data.Common.Extensions;
using Atlas.Data.Tenant;
using Microsoft.EntityFrameworkCore;
using Pomelo.EntityFrameworkCore.MySql.Internal;

namespace Atlas.Data.Tenant.Context
{
    /// <summary>
    /// Tenant数据库上下文
    /// </summary>
    public class AtlasTenantDbContext : DbContextBase
    {
        public AtlasTenantDbContext(
            DbContextOptions<AtlasTenantDbContext> options)
            : base(options)
        {
        }

        public IQueryable<TEntity> ScopedSet<TEntity>(IDataScope dataScope)
            where TEntity : class
        {
            return base.Set<TEntity>().ApplyScope(dataScope);
        }

        public IQueryable<TEntity> ScopedSet<TEntity>(DataScopeSnapshot dataScope)
            where TEntity : class
        {
            return base.Set<TEntity>().ApplyScope(dataScope);
        }

        public IQueryable<TEntity> ScopedSet<TEntity>(
            IDataScope dataScope,
            long? explicitTenantId,
            long? explicitStoreId = null)
            where TEntity : class
        {
            return base.Set<TEntity>().ApplyScope(dataScope, explicitTenantId, explicitStoreId);
        }

        public IQueryable<TEntity> ScopedSet<TEntity>(
            DataScopeSnapshot dataScope,
            long? explicitTenantId,
            long? explicitStoreId = null)
            where TEntity : class
        {
            return base.Set<TEntity>().ApplyScope(dataScope, explicitTenantId, explicitStoreId);
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // 应用租户数据项目内的实体配置。配置类型必须随运行时数据项目一起发布。
            modelBuilder.ApplyConfigurationsFromAssembly(typeof(AtlasTenantDbContext).Assembly);

            // 配置ID生成策略
            modelBuilder.ConfigureIdGenerationStrategy();
       
            // 确保外键字段有索引
            modelBuilder.EnsureForeignKeyIndexes();

            // 应用软删除过滤器
            modelBuilder.ApplySoftDeleteFilter();

            // 默认保留 EF 关系约束，避免模型层和数据库完整性策略脱节。
            modelBuilder.ApplyForeignKeyConstraintPolicy(ForeignKeyConstraintPolicy.Preserve);
        }
    }
}
