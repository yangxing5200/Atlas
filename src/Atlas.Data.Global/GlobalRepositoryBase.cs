using Atlas.Data.Abstractions;
using Atlas.Data.Common;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Core.Authorization;

namespace Atlas.Data.Global
{
    /// <summary>
    /// Global数据库仓储基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    /// <typeparam name="TKey">主键类型</typeparam>
    public class GlobalRepositoryBase<TEntity, TKey> : IRepository<TEntity, TKey>
        where TEntity : class
    {
        private const string TenantQueryNotSupportedMessage = "全局仓储不支持基于 tenantId 的查询";

        protected readonly AtlasGlobalDbContext _context;
        protected readonly DbSet<TEntity> _dbSet;

        public GlobalRepositoryBase(AtlasGlobalDbContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _dbSet = _context.Set<TEntity>();
        }

        public virtual async Task AddAsync(TEntity entity, CancellationToken ct = default)
        {
            await _dbSet.AddAsync(entity, ct);
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            await _dbSet.AddRangeAsync(entities, ct);
        }

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(id);
            return await _dbSet.FindAsync(new object[] { id }, ct);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            return await _dbSet.FirstOrDefaultAsync(predicate, ct);
        }

        public virtual async Task<List<TEntity>> ListAsync(
            Expression<Func<TEntity, bool>>? predicate = null,
            CancellationToken ct = default)
        {
            if (predicate == null)
            {
                return await _dbSet.ToListAsync(ct);
            }

            return await _dbSet.Where(predicate).ToListAsync(ct);
        }

        public virtual Task RemoveAsync(TEntity entity, CancellationToken ct = default)
        {
            _dbSet.Remove(entity);
            return Task.CompletedTask;
        }

        public virtual Task RemoveRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            _dbSet.RemoveRange(entities);
            return Task.CompletedTask;
        }

        /// <summary>
        /// 获取不可追踪查询构建器（用于只读查询）
        /// </summary>
        public virtual Task<QueryBuilder<TEntity>> QueryAsync(CancellationToken ct = default)
        {
            var query = _dbSet.AsNoTracking();
            return Task.FromResult(new QueryBuilder<TEntity>(query));
        }

        /// <summary>
        /// 获取可追踪查询构建器（用于需要更新的查询）
        /// </summary>
        public virtual Task<QueryBuilder<TEntity>> QueryTrackingAsync(CancellationToken ct = default)
        {
            var query = _dbSet.AsQueryable();
            return Task.FromResult(new QueryBuilder<TEntity>(query));
        }

        public virtual Task<QueryBuilder<TEntity>> QueryDataScopeAsync(
            string resourceCode,
            AtlasDataScopeType scopeType,
            CancellationToken ct = default)
        {
            throw new NotSupportedException("全局仓储不支持租户数据权限裁剪");
        }

        public virtual Task<QueryBuilder<TEntity>> QueryDataScopeTrackingAsync(
            string resourceCode,
            AtlasDataScopeType scopeType,
            CancellationToken ct = default)
        {
            throw new NotSupportedException("全局仓储不支持租户数据权限裁剪");
        }

        /// <summary>
        /// 全局仓储不支持基于 tenantId 的查询（全局数据库不区分租户）
        /// </summary>
        public virtual Task<QueryBuilder<TEntity>> QueryAsync(long tenantId, CancellationToken ct = default)
        {
            throw new NotSupportedException(TenantQueryNotSupportedMessage);
        }

        /// <summary>
        /// 全局仓储不支持基于 tenantId 的查询（全局数据库不区分租户）
        /// </summary>
        public virtual Task<QueryBuilder<TEntity>> QueryTrackingAsync(long tenantId, CancellationToken ct = default)
        {
            throw new NotSupportedException(TenantQueryNotSupportedMessage);
        }

        /// <summary>
        /// 全局仓储不支持基于 tenantId 的操作（全局数据库不区分租户）
        /// </summary>
        public virtual Task AddAsync(TEntity entity, long tenantId, CancellationToken ct = default)
        {
            throw new NotSupportedException(TenantQueryNotSupportedMessage);
        }

        /// <summary>
        /// 全局仓储不支持基于 tenantId 的操作（全局数据库不区分租户）
        /// </summary>
        public virtual Task AddRangeAsync(IEnumerable<TEntity> entities, long tenantId, CancellationToken ct = default)
        {
            throw new NotSupportedException(TenantQueryNotSupportedMessage);
        }

        /// <summary>
        /// 全局仓储不支持基于 tenantId 的操作（全局数据库不区分租户）
        /// </summary>
        public virtual Task RemoveAsync(TEntity entity, long tenantId, CancellationToken ct = default)
        {
            throw new NotSupportedException(TenantQueryNotSupportedMessage);
        }

        /// <summary>
        /// 全局仓储不支持基于 tenantId 的操作（全局数据库不区分租户）
        /// </summary>
        public virtual Task RemoveRangeAsync(IEnumerable<TEntity> entities, long tenantId, CancellationToken ct = default)
        {
            throw new NotSupportedException(TenantQueryNotSupportedMessage);
        }
    }

    /// <summary>
    /// Global数据库仓储基类（默认主键为long）
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public class GlobalRepositoryBase<TEntity> : GlobalRepositoryBase<TEntity, long>, IRepository<TEntity>
        where TEntity : class
    {
        public GlobalRepositoryBase(AtlasGlobalDbContext context) : base(context)
        {
        }
    }
}
