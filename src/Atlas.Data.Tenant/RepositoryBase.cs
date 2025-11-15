using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// 仓储基类
    /// </summary>
    /// <typeparam name="TEntity">实体类型</typeparam>
    public abstract class RepositoryBase<TEntity, TKey> : IRepository<TEntity, TKey>
         where TEntity : class, IBaseEntity<TKey>
         where TKey : IEquatable<TKey>
    {
        protected readonly AtlasTenantDbContext _writeContext;
        private readonly ITenantDbContextFactory _dbContextFactory;
        private readonly ICurrentIdentity _currentIdentity;
        private AtlasTenantDbContext? _readContext;

        // 静态缓存：避免重复反射
        private static readonly bool _isStoreOnlyEntity = typeof(IStoreOnlyEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isSharedEntity = typeof(ISharedEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isStoreScopedEntity = _isStoreOnlyEntity || _isSharedEntity;

        // 请求级缓存：门店ID列表每次请求只异步加载一次
        private List<long>? _accessibleStoreIds;
        private Task<List<long>>? _accessibleStoreIdsTask;

        protected RepositoryBase(
            AtlasTenantDbContext writeContext,
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity)
        {
            _writeContext = writeContext;
            _dbContextFactory = dbContextFactory;
            _currentIdentity = currentIdentity;
        }

        private AtlasTenantDbContext GetReadContext()
        {
            return _readContext ??= _dbContextFactory.CreateReadonlyDbContextSync();
        }

        /// <summary>
        /// 异步获取可访问的门店ID列表（带请求级缓存，避免重复调用）
        /// </summary>
        private async Task<List<long>> GetAccessibleStoreIdsAsync()
        {
            if (_accessibleStoreIds != null)
            {
                return _accessibleStoreIds;
            }

            // 避免并发调用时重复请求
            _accessibleStoreIdsTask ??= _currentIdentity.GetAccessibleStoreIdsAsync();

            _accessibleStoreIds = await _accessibleStoreIdsTask;
            return _accessibleStoreIds;
        }

        /// <summary>
        /// 应用门店范围过滤（异步版本，在执行查询前调用）
        /// </summary>
        private async Task<IQueryable<TEntity>> ApplyStoreScopeFilterAsync(IQueryable<TEntity> query)
        {
            // 快速路径：非门店相关实体
            if (!_isStoreScopedEntity)
            {
                return query;
            }

            // 无门店ID时返回空结果
            if (!_currentIdentity.StoreId.HasValue)
            {
                return query.Where(_ => false);
            }

            var currentStoreId = _currentIdentity.StoreId.Value;

            // IStoreOnlyEntity：仅当前门店
            if (_isStoreOnlyEntity)
            {
                return ((IQueryable<IStoreOnlyEntity>)query)
                    .Where(e => e.StoreId == currentStoreId)
                    .Cast<TEntity>();
            }

            // ISharedEntity：共享范围门店
            if (_isSharedEntity)
            {
                var accessibleStoreIds = await GetAccessibleStoreIdsAsync();

                if (accessibleStoreIds.Count == 0)
                {
                    return query.Where(_ => false);
                }

                return ((IQueryable<ISharedEntity>)query)
                    .Where(e => accessibleStoreIds.Contains(e.StoreId))
                    .Cast<TEntity>();
            }

            return query;
        }

        // ========== 基本查询 ==========

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(predicate, ct);
        }

        public virtual async Task<List<TEntity>> GetAllAsync(CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.ToListAsync(ct);
        }

        public virtual async Task<List<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Where(predicate).ToListAsync(ct);
        }

        // ========== 分页查询 ==========

        public virtual async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            var countTask = query.CountAsync(ct);
            var itemsTask = query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            await Task.WhenAll(countTask, itemsTask);

            return (itemsTask.Result, countTask.Result);
        }

        public virtual async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            Expression<Func<TEntity, bool>> predicate,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);

            var countTask = query.CountAsync(ct);
            var itemsTask = query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            await Task.WhenAll(countTask, itemsTask);

            return (itemsTask.Result, countTask.Result);
        }

        public virtual async Task<(List<TEntity> Items, int Total)> GetPagedAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);

            var countTask = query.CountAsync(ct);

            var orderedQuery = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            var itemsTask = orderedQuery
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            await Task.WhenAll(countTask, itemsTask);

            return (itemsTask.Result, countTask.Result);
        }

        // ========== 统计 ==========

        public virtual async Task<int> CountAsync(CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.CountAsync(ct);
        }

        public virtual async Task<int> CountAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.CountAsync(predicate, ct);
        }

        public virtual async Task<long> LongCountAsync(CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.LongCountAsync(ct);
        }

        public virtual async Task<long> LongCountAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.LongCountAsync(predicate, ct);
        }

        public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.AnyAsync(e => e.Id.Equals(id), ct);
        }

        public virtual async Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.AnyAsync(predicate, ct);
        }

        // ========== 聚合 ==========

        public virtual async Task<TResult?> MaxAsync<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
            {
                return default;
            }
            return await query.MaxAsync(selector, ct);
        }

        public virtual async Task<TResult?> MinAsync<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
            {
                return default;
            }
            return await query.MinAsync(selector, ct);
        }

        public virtual async Task<decimal> SumAsync(
            Expression<Func<TEntity, decimal>> selector,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.SumAsync(selector, ct);
        }

        public virtual async Task<decimal> AverageAsync(
            Expression<Func<TEntity, decimal>> selector,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
            {
                return 0;
            }
            return await query.AverageAsync(selector, ct);
        }

        // ========== 添加 ==========

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
        {
            await _writeContext.Set<TEntity>().AddAsync(entity, ct);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();

            await _writeContext.Set<TEntity>().AddRangeAsync(entityList, ct);
        }

        // ========== 更新 ==========

        public virtual Task UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            _writeContext.Entry(entity).State = EntityState.Modified;
            return Task.CompletedTask;
        }

        public virtual Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();

            foreach (var entity in entityList)
            {
                _writeContext.Entry(entity).State = EntityState.Modified;
            }
            return Task.CompletedTask;
        }

        // ========== 删除 ==========

        public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken ct = default)
        {
            // 删除操作从主库查询
            var query = _writeContext.Set<TEntity>().AsQueryable();
            query = await ApplyStoreScopeFilterAsync(query);

            var entity = await query.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);

            if (entity == null)
            {
                return false;
            }

            _writeContext.Set<TEntity>().Remove(entity);
            return true;
        }

        public virtual Task DeleteAsync(TEntity entity, CancellationToken ct = default)
        {
            _writeContext.Set<TEntity>().Remove(entity);
            return Task.CompletedTask;
        }

        public virtual Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();
            _writeContext.Set<TEntity>().RemoveRange(entityList);
            return Task.CompletedTask;
        }

        // ========== 高级查询 ==========

        public virtual async Task<List<TEntity>> FindAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending = true,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await query.ToListAsync(ct);
        }

        public virtual async Task<List<TEntity>> GetTopAsync(int count, CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Take(count).ToListAsync(ct);
        }

        public virtual async Task<List<TEntity>> GetTopAsync(
            Expression<Func<TEntity, bool>> predicate,
            int count,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Where(predicate).Take(count).ToListAsync(ct);
        }

        public virtual async Task<List<TEntity>> GetTopAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending,
            int count,
            CancellationToken ct = default)
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await query.Take(count).ToListAsync(ct);
        }

        // ========== 原始查询访问 ==========

        /// <summary>
        /// 获取只读查询（自动应用门店过滤）
        /// 注意：返回的 IQueryable 在实际执行前需要先异步初始化门店过滤
        /// 建议使用具体的查询方法而非直接使用此方法
        /// </summary>
        public virtual IQueryable<TEntity> AsReadonlyQueryable()
        {
            // 警告：此方法返回的 IQueryable 尚未应用门店过滤
            // 门店过滤需要异步获取，但 IQueryable 构建是同步的
            // 调用方必须通过 Repository 的查询方法来确保正确应用过滤
            var query = GetReadContext().Set<TEntity>().AsNoTracking();

            // 同步应用过滤（仅适用于已缓存的场景）
            if (_accessibleStoreIds != null)
            {
                return ApplyStoreScopeFilterSync(query);
            }

            return query;
        }

        /// <summary>
        /// 获取可跟踪查询（自动应用门店过滤）
        /// 注意：返回的 IQueryable 在实际执行前需要先异步初始化门店过滤
        /// 建议使用具体的查询方法而非直接使用此方法
        /// </summary>
        public virtual IQueryable<TEntity> AsQueryable()
        {
            var query = _writeContext.Set<TEntity>().AsQueryable();

            if (_accessibleStoreIds != null)
            {
                return ApplyStoreScopeFilterSync(query);
            }

            return query;
        }

        public virtual IQueryable<TEntity> AsReadonlyQueryableUnfiltered()
        {
            return GetReadContext().Set<TEntity>().AsNoTracking();
        }

        public virtual IQueryable<TEntity> AsQueryableUnfiltered()
        {
            return _writeContext.Set<TEntity>().AsQueryable();
        }

        /// <summary>
        /// 同步应用门店过滤（仅在门店ID已缓存时使用）
        /// </summary>
        private IQueryable<TEntity> ApplyStoreScopeFilterSync(IQueryable<TEntity> query)
        {
            if (!_isStoreScopedEntity || !_currentIdentity.StoreId.HasValue)
            {
                return query;
            }

            var currentStoreId = _currentIdentity.StoreId.Value;

            if (_isStoreOnlyEntity)
            {
                return ((IQueryable<IStoreOnlyEntity>)query)
                    .Where(e => e.StoreId == currentStoreId)
                    .Cast<TEntity>();
            }

            if (_isSharedEntity && _accessibleStoreIds != null && _accessibleStoreIds.Count > 0)
            {
                return ((IQueryable<ISharedEntity>)query)
                    .Where(e => _accessibleStoreIds.Contains(e.StoreId))
                    .Cast<TEntity>();
            }

            return query;
        }

        // ========== 保存 ==========

        public virtual async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            return await _writeContext.SaveChangesAsync(ct);
        }

        public virtual async Task<TEntity?> GetForUpdateAsync(TKey id, CancellationToken ct = default)
        {
            var query = _writeContext.Set<TEntity>().AsQueryable();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);
        }

        public virtual void Dispose()
        {
            _readContext?.Dispose();
        }
    }
}