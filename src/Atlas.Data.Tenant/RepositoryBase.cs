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
        private readonly ITenantDbContextFactory _dbContextFactory;
        private readonly ICurrentIdentity _currentIdentity;
        private AtlasTenantDbContext? _writeContext;
        private AtlasTenantDbContext? _readContext;
        private Task<AtlasTenantDbContext>? _writeContextTask;

        // 静态缓存：避免重复反射
        private static readonly bool _isStoreOnlyEntity = typeof(IStoreOnlyEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isSharedEntity = typeof(ISharedEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isStoreScopedEntity = _isStoreOnlyEntity || _isSharedEntity;

        // 请求级缓存：门店ID列表每次请求只异步加载一次
        private List<long>? _accessibleStoreIds;
        private Task<List<long>>? _accessibleStoreIdsTask;
        private long? _cachedForStoreId;
        protected RepositoryBase(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        }

        private AtlasTenantDbContext GetReadContext()
        {
            return _readContext ??= _dbContextFactory.CreateReadonlyDbContextSync();
        }

        private async Task<AtlasTenantDbContext> GetWriteContextAsync()
        {
            if (_writeContext != null)
                return _writeContext;

            _writeContextTask ??= _dbContextFactory.CreateDbContextAsync();
            _writeContext = await _writeContextTask;
            return _writeContext;
        }

        // <summary>
        /// 异步获取可访问的门店ID列表（带请求级缓存，避免重复调用）
        /// </summary>
        private async Task<List<long>> GetAccessibleStoreIdsAsync()
        {
            var currentStoreId = _currentIdentity.StoreId;

            // 检测 storeId 是否变化，变化则清除缓存
            if (_cachedForStoreId != currentStoreId)
            {
                _accessibleStoreIds = null;
                _accessibleStoreIdsTask = null;
                _cachedForStoreId = currentStoreId;
            }

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

            if (typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity)))
            {
                if (!_currentIdentity.TenantId.HasValue)
                {
                    // 无租户ID时返回空结果
                    return query.Where(_ => false);
                }

                var currentTenantId = _currentIdentity.TenantId.Value;
                query = ((IQueryable<ITenantEntity>)query)
                    .Where(e => e.TenantId == currentTenantId)
                    .Cast<TEntity>();
            }

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
            var query = GetReadContext().GetDbSet<TEntity>().AsNoTracking();
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
            var context = await GetWriteContextAsync();
            await context.Set<TEntity>().AddAsync(entity, ct);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();
            var context = await GetWriteContextAsync();
            await context.Set<TEntity>().AddRangeAsync(entityList, ct);
        }

        // ========== 更新 ==========

        public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            var context = await GetWriteContextAsync();
            context.Entry(entity).State = EntityState.Modified;
        }

        public virtual async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();
            var context = await GetWriteContextAsync();
            foreach (var entity in entityList)
            {
                context.Entry(entity).State = EntityState.Modified;
            }
        }

        // ========== 删除 ==========

        public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken ct = default)
        {
            var context = await GetWriteContextAsync();
            var query = context.Set<TEntity>().AsQueryable();
            query = await ApplyStoreScopeFilterAsync(query);

            var entity = await query.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);

            if (entity == null)
            {
                return false;
            }

            context.Set<TEntity>().Remove(entity);
            return true;
        }

        public virtual async Task DeleteAsync(TEntity entity, CancellationToken ct = default)
        {
            var context = await GetWriteContextAsync();
            context.Set<TEntity>().Remove(entity);
        }

        public virtual async Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();
            var context = await GetWriteContextAsync();
            context.Set<TEntity>().RemoveRange(entityList);
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

        public virtual IQueryable<TEntity> AsReadonlyQueryable()
        {
            var query = GetReadContext().Set<TEntity>().AsNoTracking();

            if (_accessibleStoreIds != null)
            {
                return ApplyStoreScopeFilterSync(query);
            }

            return query;
        }

        public virtual async Task<IQueryable<TEntity>> AsQueryable()
        {
            var context = await GetWriteContextAsync();
            var query = context.Set<TEntity>().AsQueryable();

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

        public virtual async Task<IQueryable<TEntity>> AsQueryableUnfiltered()
        {
            var context = await GetWriteContextAsync();
            return context.Set<TEntity>().AsQueryable();
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
            var context = await GetWriteContextAsync();
            return await context.SaveChangesAsync(ct);
        }

        public virtual async Task<TEntity?> GetForUpdateAsync(TKey id, CancellationToken ct = default)
        {
            var context = await GetWriteContextAsync();
            var query = context.Set<TEntity>().AsQueryable();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);
        }

        public virtual void Dispose()
        {
            _writeContext?.Dispose();
            _readContext?.Dispose();
        }
    }
    public abstract class RepositoryBase<TEntity>
        : RepositoryBase<TEntity, long>, IRepository<TEntity>
        where TEntity : class, IBaseEntity<long>
    {
        protected RepositoryBase(ITenantDbContextFactory dbContextFactory, ICurrentIdentity currentIdentity) : base(dbContextFactory, currentIdentity)
        {
        }
    }
}