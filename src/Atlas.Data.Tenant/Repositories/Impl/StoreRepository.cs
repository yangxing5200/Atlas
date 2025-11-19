using Atlas.Core.Context;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Repositories;
using Atlas.Models.Tenant.Entities;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Repositories.Impl
{
    public class StoreRepository : Repository<Store>, IStoreRepository
    {
        public StoreRepository(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator)
            : base( dbContextFactory, currentIdentity, idGenerator)
        {
        }

        public async Task<List<Store>> GetChildDirectStoresAsync(long parentStoreId, CancellationToken ct = default)
        {
            return await AsReadonlyQueryable()
                 .Where(s => s.ParentStoreId == parentStoreId && s.Type == StoreType.DirectOperated)
                 .ToListAsync(ct);
        }

        public async Task<List<long>> GetChildStoreIdsAsync(long parentStoreId, CancellationToken ct = default)
        {
            return await AsReadonlyQueryable()
              .Where(s => s.ParentStoreId == parentStoreId)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        public async Task<List<Store>> GetSiblingDirectStoresAsync(long parentStoreId, CancellationToken ct = default)
        {
            return await AsReadonlyQueryable()
                 .Where(s => s.ParentStoreId == parentStoreId && s.Type == StoreType.DirectOperated)
                .ToListAsync(ct);
        }
    }
}