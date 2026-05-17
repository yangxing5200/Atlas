using Atlas.Core.Entities.Interfaces;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Models.Tenant.Responses;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Repositories
{
    #region RepositoryBase Implementation

    /// <summary>
    /// 租户库仓储基类，统一处理查询范围、租户写入校验和软删除。
    /// </summary>
    /// <remarks>
    /// 派生仓储只应补充特定聚合的查询方法；基础 CRUD 必须经过这里，以保持租户隔离策略一致。
    /// </remarks>
    public abstract class RepositoryBase<TEntity, TKey> : IRepository<TEntity, TKey>
        where TEntity : class, IBaseEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        protected readonly ITenantDbContextFactory _dbFactory;
        protected readonly IDataScope _dataScope;

        protected RepositoryBase(ITenantDbContextFactory dbFactory, IDataScope dataScope)
        {
            _dbFactory = dbFactory ?? throw new ArgumentNullException(nameof(dbFactory));
            _dataScope = dataScope ?? throw new ArgumentNullException(nameof(dataScope));
        }

        #region Add

        public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            // 写入租户实体前先绑定或校验 TenantId，避免实体被写入错误租户库。
            EnsureCurrentTenant(entity);
            var db = await _dbFactory.GetDbContextAsync(ct);
            await db.Set<TEntity>().AddAsync(entity, ct);
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                EnsureCurrentTenant(entity);
            }

            var db = await _dbFactory.GetDbContextAsync(ct);
            await db.Set<TEntity>().AddRangeAsync(entityList, ct);
        }

        /// <summary>
        /// 添加实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        public virtual async Task AddAsync(TEntity entity, long tenantId, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            EnsureExplicitTenant(entity, tenantId);
            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            await db.Set<TEntity>().AddAsync(entity, ct);
        }

        /// <summary>
        /// 批量添加实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, long tenantId, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                EnsureExplicitTenant(entity, tenantId);
            }

            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            await db.Set<TEntity>().AddRangeAsync(entityList, ct);
        }

        #endregion

        #region Query

        /// <summary>
        /// 获取只读查询构建器（AsNoTracking）
        /// </summary>
        public virtual async Task<QueryBuilder<TEntity>> QueryAsync(CancellationToken ct = default)
        {
            var db = await _dbFactory.GetReadonlyDbContextAsync(ct);
            var scope = await _dataScope.ResolveAsync(ct);
            var query = db.ScopedSet<TEntity>(scope)
                .AsNoTracking();
            return new QueryBuilder<TEntity>(query);
        }

        /// <summary>
        /// 获取可追踪查询构建器（用于后续更新）
        /// </summary>
        public virtual async Task<QueryBuilder<TEntity>> QueryTrackingAsync(CancellationToken ct = default)
        {
            var db = await _dbFactory.GetDbContextAsync(ct);
            var scope = await _dataScope.ResolveAsync(ct);
            var query = db.ScopedSet<TEntity>(scope);
            return new QueryBuilder<TEntity>(query);
        }

        /// <summary>
        /// 获取只读查询构建器（AsNoTracking）- 显式传入 tenantId
        /// 用于登录等无 Token 上下文的场景
        /// </summary>
        public virtual async Task<QueryBuilder<TEntity>> QueryAsync(long tenantId, CancellationToken ct = default)
        {
            var db = await _dbFactory.GetReadonlyDbContextAsync(tenantId, ct);
            var scope = await _dataScope.ResolveAsync(ct);
            var query = db.ScopedSet<TEntity>(scope, explicitTenantId: tenantId)
                .AsNoTracking();
            return new QueryBuilder<TEntity>(query);
        }

        /// <summary>
        /// 获取可追踪查询构建器（用于后续更新）- 显式传入 tenantId
        /// 用于登录等无 Token 上下文的场景
        /// </summary>
        public virtual async Task<QueryBuilder<TEntity>> QueryTrackingAsync(long tenantId, CancellationToken ct = default)
        {
            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            var scope = await _dataScope.ResolveAsync(ct);
            var query = db.ScopedSet<TEntity>(scope, explicitTenantId: tenantId);
            return new QueryBuilder<TEntity>(query);
        }

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default)
        {
            var db = await _dbFactory.GetReportDbContextAsync(ct);
            var scope = await _dataScope.ResolveAsync(ct);
            return await db.ScopedSet<TEntity>(scope)
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id.Equals(id), ct);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(Expression<Func<TEntity, bool>> predicate, CancellationToken ct = default)
        {
            var builder = await QueryAsync(ct);
            return await builder.Where(predicate).FirstOrDefaultAsync(ct);
        }

        public virtual async Task<List<TEntity>> ListAsync(Expression<Func<TEntity, bool>>? predicate = null, CancellationToken ct = default)
        {
            var builder = await QueryAsync(ct);
            if (predicate != null)
                builder = builder.Where(predicate);
            return await builder.ToListAsync(ct);
        }


        #endregion

        #region Remove

        public virtual async Task RemoveAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            EnsureCurrentTenant(entity);
            var db = await _dbFactory.GetDbContextAsync(ct);
            MarkForDeletion(db, entity);
        }

        public virtual async Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                EnsureCurrentTenant(entity);
            }

            var db = await _dbFactory.GetDbContextAsync(ct);
            foreach (var entity in entityList)
            {
                MarkForDeletion(db, entity);
            }
        }

        /// <summary>
        /// 删除实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        public virtual async Task RemoveAsync(TEntity entity, long tenantId, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            EnsureExplicitTenant(entity, tenantId);
            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            MarkForDeletion(db, entity);
        }

        /// <summary>
        /// 批量删除实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        public virtual async Task RemoveRangeAsync(IEnumerable<TEntity> entities, long tenantId, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var entityList = entities.ToList();
            foreach (var entity in entityList)
            {
                EnsureExplicitTenant(entity, tenantId);
            }

            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            foreach (var entity in entityList)
            {
                MarkForDeletion(db, entity);
            }
        }

        private static void EnsureExplicitTenant(TEntity entity, long tenantId)
        {
            EnsureTenant(entity, tenantId, "explicit");
        }

        private void EnsureCurrentTenant(TEntity entity)
        {
            if (entity is not ITenantEntity)
                return;

            var tenantId = _dataScope.TenantId
                ?? throw new InvalidOperationException(
                    $"Current tenant id is required to write tenant entity {typeof(TEntity).Name}. Use the explicit tenant overload for system or login flows.");

            EnsureTenant(entity, tenantId, "current");
        }

        private static void EnsureTenant(TEntity entity, long tenantId, string source)
        {
            if (entity is not ITenantEntity tenantEntity)
                return;

            if (tenantEntity.TenantId == 0)
            {
                tenantEntity.TenantId = tenantId;
                return;
            }

            if (tenantEntity.TenantId != tenantId)
            {
                throw new InvalidOperationException(
                    $"Entity {typeof(TEntity).Name} tenant id '{tenantEntity.TenantId}' does not match {source} tenant id '{tenantId}'.");
            }
        }

        /// <summary>
        /// 根据实体能力选择软删除或物理删除。
        /// </summary>
        private static void MarkForDeletion(AtlasTenantDbContext db, TEntity entity)
        {
            if (entity is not ISoftDelete softDelete)
            {
                db.Set<TEntity>().Remove(entity);
                return;
            }

            var entry = db.Entry(entity);
            if (entry.State == EntityState.Detached)
            {
                db.Set<TEntity>().Attach(entity);
            }

            softDelete.IsDeleted = true;
            softDelete.DeletedAt ??= DateTime.UtcNow;
            db.Entry(entity).State = EntityState.Modified;
        }

        #endregion
    }

    public class RepositoryBase<TEntity> : RepositoryBase<TEntity, long>, IRepository<TEntity>
        where TEntity : class, IBaseEntity<long>
    {
        public RepositoryBase(ITenantDbContextFactory dbFactory, IDataScope dataScope) : base(dbFactory, dataScope)
        {
        }
    }

    #endregion
}
