using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Repositories
{
    /// <summary>
    /// 仓储基类 - 支持读写分离
    /// </summary>
    public abstract class RepositoryBase<TEntity, TKey> : RepositoryOperationsBase<TEntity, TKey>
         where TEntity : class, IBaseEntity<TKey>
         where TKey : IEquatable<TKey>
    {
        private readonly ITenantDbContextFactory _dbContextFactory;
        private readonly AccessibleStoreIdsCache _storeIdsCache;

        private AtlasTenantDbContext? _writeContext;
        private AtlasTenantDbContext? _readContext;
        private Task<AtlasTenantDbContext>? _writeContextTask;

        protected RepositoryBase(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(currentIdentity, idGenerator)
        {
            _dbContextFactory = dbContextFactory ?? throw new ArgumentNullException(nameof(dbContextFactory));
            _storeIdsCache = new AccessibleStoreIdsCache(currentIdentity);
        }

        private AtlasTenantDbContext GetReadContextSync()
        {
            return _readContext ??= _dbContextFactory.GetReadonlyDbContext();
        }

        protected override async Task<AtlasTenantDbContext> GetContextAsync()
        {
            if (_writeContext != null)
                return _writeContext;

            _writeContextTask ??= _dbContextFactory.GetMasterDbContextAsync();
            _writeContext = await _writeContextTask;
            return _writeContext;
        }

        protected override Task<AtlasTenantDbContext> GetReadContextAsync()
        {
            return Task.FromResult(GetReadContextSync());
        }

        protected override async Task<IQueryable<TEntity>> ApplyStoreScopeFilterAsync(IQueryable<TEntity> query)
        {
            var cachedStoreIds = EntityScopeFilter<TEntity>.IsSharedEntity
                ? await _storeIdsCache.GetAsync()
                : null;

            return await EntityScopeFilter<TEntity>.ApplyAsync(
                query,
                _currentIdentity,
                cachedStoreIds);
        }

        // ========== 同步查询方法 ==========

        public override IQueryable<TEntity> AsReadonlyQueryable()
        {
            var query = GetReadContextSync().Set<TEntity>().AsNoTracking();
            var cachedStoreIds = _storeIdsCache.GetCached();

            if (cachedStoreIds != null || !EntityScopeFilter<TEntity>.IsSharedEntity)
            {
                return EntityScopeFilter<TEntity>.Apply(query, _currentIdentity, cachedStoreIds);
            }

            return query;
        }

        public override IQueryable<TEntity> AsReadonlyQueryableUnfiltered()
        {
            return GetReadContextSync().Set<TEntity>().AsNoTracking();
        }

        // ========== 保存 ==========

        public override async Task<int> SaveChangesAsync(CancellationToken ct = default)
        {
            var context = await GetContextAsync();
            return await context.SaveChangesAsync(ct);
        }

        public override void Dispose()
        {
            _writeContext?.Dispose();
            _readContext?.Dispose();
        }
    }

    /// <summary>
    /// 默认 Repository 实现（使用 long 类型主键）
    /// </summary>
    public class RepositoryBase<TEntity> : RepositoryBase<TEntity, long>, IRepository<TEntity>
        where TEntity : class, IBaseEntity<long>
    {
        public RepositoryBase(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base(dbContextFactory, currentIdentity, idGenerator)
        {
        }
    }
}