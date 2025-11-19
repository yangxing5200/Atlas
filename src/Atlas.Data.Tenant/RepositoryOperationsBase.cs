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
    /// Repository 操作的共享基类 - 包含所有通用的 CRUD 实现
    /// 子类只需实现 GetContextAsync 方法
    /// </summary>
    public abstract class RepositoryOperationsBase<TEntity, TKey> : IRepository<TEntity, TKey>
        where TEntity : class, IBaseEntity<TKey>
        where TKey : IEquatable<TKey>
    {
        protected readonly ICurrentIdentity CurrentIdentity;
        protected readonly IIdGenerator IdGenerator;

        protected RepositoryOperationsBase(
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
        {
            CurrentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
            IdGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        }

        /// <summary>
        /// 获取 DbContext（子类实现）
        /// </summary>
        protected abstract Task<AtlasTenantDbContext> GetContextAsync();

        /// <summary>
        /// 获取只读 DbContext（可选，用于读写分离优化）
        /// </summary>
        protected virtual Task<AtlasTenantDbContext> GetReadContextAsync() => GetContextAsync();

        /// <summary>
        /// 应用门店范围过滤
        /// </summary>
        protected virtual async Task<IQueryable<TEntity>> ApplyStoreScopeFilterAsync(IQueryable<TEntity> query)
        {
            return await EntityScopeFilter<TEntity>.ApplyFilterAsync(query, CurrentIdentity);
        }

        // ========== 基本查询 ==========

        public virtual async Task<TEntity?> GetByIdAsync(TKey id, CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);
        }

        public virtual async Task<TEntity?> FirstOrDefaultAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(predicate, ct);
        }

        public virtual async Task<List<TEntity>> GetAllAsync(CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.ToListAsync(ct);
        }

        public virtual async Task<List<TEntity>> ReadOnlyQueryAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Where(predicate).ToListAsync(ct);
        }

        public virtual async Task<List<TEntity>> QueryWithTrackingAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Where(predicate).ToListAsync(ct);
        }

        // ========== 分页查询 ==========

        public virtual async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            var total = await query.CountAsync(ct);
            var items = await query
                .Skip((pageIndex - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync(ct);

            return (items, total);
        }

        public virtual async Task<(List<TEntity> Items, int Total)> GetPagedAsync(
            Expression<Func<TEntity, bool>> predicate,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
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

        public virtual async Task<(List<TEntity> Items, int Total)> GetPagedAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending,
            int pageIndex,
            int pageSize,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
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

        public virtual async Task<int> CountAsync(CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.CountAsync(ct);
        }

        public virtual async Task<int> CountAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.CountAsync(predicate, ct);
        }

        public virtual async Task<long> LongCountAsync(CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.LongCountAsync(ct);
        }

        public virtual async Task<long> LongCountAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.LongCountAsync(predicate, ct);
        }

        public virtual async Task<bool> ExistsAsync(TKey id, CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.AnyAsync(e => e.Id.Equals(id), ct);
        }

        public virtual async Task<bool> AnyAsync(
            Expression<Func<TEntity, bool>> predicate,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.AnyAsync(predicate, ct);
        }

        // ========== 聚合 ==========

        public virtual async Task<TResult?> MaxAsync<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
                return default;

            return await query.MaxAsync(selector, ct);
        }

        public virtual async Task<TResult?> MinAsync<TResult>(
            Expression<Func<TEntity, TResult>> selector,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
                return default;

            return await query.MinAsync(selector, ct);
        }

        public virtual async Task<decimal> SumAsync(
            Expression<Func<TEntity, decimal>> selector,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.SumAsync(selector, ct);
        }

        public virtual async Task<decimal> AverageAsync(
            Expression<Func<TEntity, decimal>> selector,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);

            if (!await query.AnyAsync(ct))
                return 0;

            return await query.AverageAsync(selector, ct);
        }

        // ========== 添加 ==========

        public virtual async Task<TEntity> AddAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity is ISnowflakeId se && se.Id == 0)
            {
                se.Id = IdGenerator.NextId();
            }

            var context = await GetContextAsync();
            await context.Set<TEntity>().AddAsync(entity, ct);
            return entity;
        }

        public virtual async Task AddRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities.ToList();

            foreach (var entity in entityList)
            {
                if (entity is ISnowflakeId se && se.Id == 0)
                {
                    se.Id = IdGenerator.NextId();
                }
            }

            var context = await GetContextAsync();
            await context.Set<TEntity>().AddRangeAsync(entityList, ct);
        }

        // ========== 更新 ==========

        public virtual async Task UpdateAsync(TEntity entity, CancellationToken ct = default)
        {
            if (entity == null)
                throw new ArgumentNullException(nameof(entity));

            var context = await GetContextAsync();
            var entry = context.Entry(entity);

            switch (entry.State)
            {
                case EntityState.Detached:
                    context.Update(entity);
                    break;

                case EntityState.Unchanged:
                    entry.State = EntityState.Modified;
                    break;

                case EntityState.Modified:
                case EntityState.Added:
                    break;

                case EntityState.Deleted:
                    throw new InvalidOperationException(
                        $"Cannot update entity of type {typeof(TEntity).Name} that is marked for deletion.");

                default:
                    throw new ArgumentOutOfRangeException();
            }

            await Task.CompletedTask;
        }

        public virtual async Task UpdateRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
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

        public virtual async Task<bool> DeleteAsync(TKey id, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsQueryable();
            query = await ApplyStoreScopeFilterAsync(query);

            var entity = await query.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);
            if (entity == null)
                return false;

            context.Set<TEntity>().Remove(entity);
            return true;
        }

        public virtual async Task DeleteAsync(TEntity entity, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            context.Set<TEntity>().Remove(entity);
            await Task.CompletedTask;
        }

        public virtual async Task DeleteRangeAsync(IEnumerable<TEntity> entities, CancellationToken ct = default)
        {
            var entityList = entities as IReadOnlyCollection<TEntity> ?? entities.ToList();
            var context = await GetContextAsync();
            context.Set<TEntity>().RemoveRange(entityList);
            await Task.CompletedTask;
        }

        // ========== 高级查询 ==========

        public virtual async Task<List<TEntity>> FindAsync<TProperty>(
            Expression<Func<TEntity, bool>> predicate,
            Expression<Func<TEntity, TProperty>> orderBy,
            bool ascending = true,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await query.ToListAsync(ct);
        }

        public virtual async Task<List<TEntity>> GetTopAsync(int count, CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.Take(count).ToListAsync(ct);
        }

        public virtual async Task<List<TEntity>> GetTopAsync(
            Expression<Func<TEntity, bool>> predicate,
            int count,
            CancellationToken ct = default)
        {
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
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
            var context = await GetReadContextAsync();
            var query = context.Set<TEntity>().AsNoTracking();
            query = await ApplyStoreScopeFilterAsync(query);
            query = query.Where(predicate);
            query = ascending ? query.OrderBy(orderBy) : query.OrderByDescending(orderBy);
            return await query.Take(count).ToListAsync(ct);
        }

        // ========== 原始查询访问 ==========

        public virtual async Task<IQueryable<TEntity>> AsQueryable()
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsQueryable();
            return await ApplyStoreScopeFilterAsync(query);
        }

        public virtual async Task<IQueryable<TEntity>> AsQueryableUnfiltered()
        {
            var context = await GetContextAsync();
            return context.Set<TEntity>().AsQueryable();
        }

        public abstract IQueryable<TEntity> AsReadonlyQueryable();

        public abstract IQueryable<TEntity> AsReadonlyQueryableUnfiltered();

        // ========== 保存 ==========

        public abstract Task<int> SaveChangesAsync(CancellationToken ct = default);

        public virtual async Task<TEntity?> GetForUpdateAsync(TKey id, CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            var query = context.Set<TEntity>().AsQueryable();
            query = await ApplyStoreScopeFilterAsync(query);
            return await query.FirstOrDefaultAsync(e => e.Id.Equals(id), ct);
        }

        public abstract void Dispose();
    }
}

