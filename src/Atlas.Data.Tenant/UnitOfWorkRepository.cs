using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// UnitOfWork 专用的轻量级 Repository 包装器
    /// 仅实现常用方法，完整实现参考 RepositoryBase
    /// </summary>
    /// <summary>
    /// UnitOfWork 专用的 Repository 实现
    /// 使用 UnitOfWork 提供的共享 DbContext
    /// </summary>
    internal class UnitOfWorkRepository<TEntity> : IRepository<TEntity>
        where TEntity : class, IBaseEntity<long>
    {
        private readonly UnitOfWork _unitOfWork;
        private readonly ICurrentIdentity _currentIdentity;
        private readonly IIdGenerator _idGenerator;

        // 静态缓存：避免重复反射
        private static readonly bool _isStoreOnlyEntity = typeof(IStoreOnlyEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isSharedEntity = typeof(ISharedEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isTenantEntity = typeof(ITenantEntity).IsAssignableFrom(typeof(TEntity));
        private static readonly bool _isStoreScopedEntity = _isStoreOnlyEntity || _isSharedEntity;

        public UnitOfWorkRepository(
            UnitOfWork unitOfWork,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
        {
            _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
            _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
            _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        }

        private async Task<AtlasTenantDbContext> GetContextAsync()
        {
            return await _unitOfWork.GetDbContextAsync();
        }

        // ========== 基本查询 ==========

        public async Task<TEntity?> GetByIdAsync(long id, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
        }

        public async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(predicate, ct);
        }

        public async Task<List<TEntity>> GetAllAsync(CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.ToListAsync(ct);
        }

        public async Task<List<TEntity>> FindAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Where(predicate).ToListAsync(ct);
        }

        // ========== 分页查询 ==========

        public async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            Expression<Func<TEntity, bool>> predicate,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public async Task<(List<TEntity> Items, int Total)> GetPagedAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);

            var total = await query.CountAsync(ct);
            var orderedQuery = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            var items = await orderedQuery
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        // ========== 统计 ==========

        public async Task<int> CountAsync(CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.CountAsync(ct);
        }

        public async Task<int> CountAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.CountAsync(predicate, ct);
        }

        public async Task<bool> ExistsAsync(long id, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.AnyAsync(e => e.Id == id, ct);
        }

        public async Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.AnyAsync(predicate, ct);
        }

        // ========== 添加 ==========

        public async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity is ISnowflakeId se && se.Id == 0)
            {
                se.Id = _idGenerator.NextId();
            }

            var context = await GetContextAsync();
            await context.Set<TEntity>().AddAsync(entity, ct);
            return entity;
        }

        public async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities.ToList();

            foreach (var entity in entityList)
            {
                if (entity is ISnowflakeId se && se.Id == 0)
                {
                    se.Id = _idGenerator.NextId();
                }
            }

            var context = await GetContextAsync();
            await context.Set<TEntity>().AddRangeAsync(entityList, ct);
        }

        // ========== 更新 ==========

        public async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            context.Entry(entity).State = EntityState.Modified;
            await Task.CompletedTask;
        }

        public async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();
            var context = await GetContextAsync();

            foreach (var entity in entityList)
            {
                context.Entry(entity).State = EntityState.Modified;
            }

            await Task.CompletedTask;
        }

        // ========== 删除 ==========

        public async Task<bool> DeleteAsync(long id, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsQueryable();
            query = await ApplyStoreScopeFilterAsync(query);

            var entity = await query.FirstOrDefaultAsync(e => e.Id == id, ct);
            if (entity == null)
            {
                return false;
            }

            context.Set<TEntity>().Remove(entity);
            return true;
        }

        public async Task DeleteAsync(TEntity entity, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            context.Set<TEntity>().Remove(entity);
            await Task.CompletedTask;
        }

        public async Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();
            var context = await GetContextAsync();
            context.Set<TEntity>().RemoveRange(entityList);
            await Task.CompletedTask;
        }

        // ========== 高级查询 ==========

        public async Task<List<TEntity>> FindAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending = true,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await query.ToListAsync(ct);
        }

        public async Task<List<TEntity>> GetTopAsync(int count, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Take(count).ToListAsync(ct);
        }

        public async Task<List<TEntity>> GetTopAsync(
            Expression<Func<TEntity, bool>> predicate,
            int count,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Where(predicate).Take(count).ToListAsync(ct);
        }

        public async Task<List<TEntity>> GetTopAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending,
            int count,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await query.Take(count).ToListAsync(ct);
        }

        // ========== 聚合 ==========

        public async Task<TResult?> MaxAsync<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
            {
                return default;
            }
            return await query.MaxAsync(selector, ct);
        }

        public async Task<TResult?> MinAsync<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
            {
                return default;
            }
            return await query.MinAsync(selector, ct);
        }

        public async Task<decimal> SumAsync(
            Expression<Func<TEntity, decimal>> selector,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.SumAsync(selector, ct);
        }

        public async Task<decimal> AverageAsync(
            Expression<Func<TEntity, decimal>> selector,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
            {
                return 0;
            }
            return await query.AverageAsync(selector, ct);
        }

        public async Task<long> LongCountAsync(CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.LongCountAsync(ct);
        }

        public async Task<long> LongCountAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.LongCountAsync(predicate, ct);
        }

        // ========== 原始查询访问 ==========

        public IQueryable<TEntity> AsReadonlyQueryable()
        {
            throw new NotSupportedException("UnitOfWorkRepository 不支持同步获取 Queryable，请使用 AsQueryable()");
        }

        public async Task<IQueryable<TEntity>> AsQueryable()
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsQueryable();
            return await ApplyStoreScopeFilterAsync(query);
        }

        public IQueryable<TEntity> AsReadonlyQueryableUnfiltered()
        {
            throw new NotSupportedException("UnitOfWorkRepository 不支持同步获取 Queryable");
        }

        public async Task<IQueryable<TEntity>> AsQueryableUnfiltered()
        {
            var context = await GetContextAsync();
            return context.Set<TEntity>().AsQueryable();
        }

        // ========== 保存 ==========

        public Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            return _unitOfWork.SaveChangesAsync(ct);
        }

        public async Task<TEntity?> GetForUpdateAsync(long id, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsQueryable();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(e => e.Id == id, ct);
        }

        public void Dispose()
        {
            // UnitOfWork 负责释放 DbContext
        }

        // ========== 门店过滤逻辑 ==========

        private async Task<IQueryable<TEntity>> ApplyStoreScopeFilterAsync(IQueryable<TEntity> query)
        {
            // 租户过滤
            if (_isTenantEntity)
            {
                if (!_currentIdentity.TenantId.HasValue)
                {
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
                var accessibleStoreIds = await _currentIdentity.GetAccessibleStoreIdsAsync();

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
    }
}
