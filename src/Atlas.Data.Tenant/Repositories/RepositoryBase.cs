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
            var db = await _dbFactory.GetDbContextAsync(ct);
            await db.Set<TEntity>().AddAsync(entity, ct);
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var db = await _dbFactory.GetDbContextAsync(ct);
            await db.Set<TEntity>().AddRangeAsync(entities, ct);
        }

        /// <summary>
        /// 添加实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        public virtual async Task AddAsync(TEntity entity, long tenantId, CancellationToken ct = default)
        {
            if (entity == null) throw new ArgumentNullException(nameof(entity));
            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            await db.Set<TEntity>().AddAsync(entity, ct);
        }

        /// <summary>
        /// 批量添加实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, long tenantId, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            await db.Set<TEntity>().AddRangeAsync(entities, ct);
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
            var db = await _dbFactory.GetDbContextAsync(ct);
            MarkForDeletion(db, entity);
        }

        public virtual async Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var db = await _dbFactory.GetDbContextAsync(ct);
            foreach (var entity in entities)
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
            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            MarkForDeletion(db, entity);
        }

        /// <summary>
        /// 批量删除实体（显式传入 tenantId，用于登录等场景）
        /// </summary>
        public virtual async Task RemoveRangeAsync(IEnumerable<TEntity> entities, long tenantId, CancellationToken ct = default)
        {
            if (entities == null) throw new ArgumentNullException(nameof(entities));
            var db = await _dbFactory.GetDbContextAsync(tenantId, ct);
            foreach (var entity in entities)
            {
                MarkForDeletion(db, entity);
            }
        }

        private static void MarkForDeletion(DbContext db, TEntity entity)
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
