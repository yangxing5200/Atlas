using Atlas.Core.Context;
using Atlas.Core.Enums;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Repositories;
using Atlas.Models.Tenant.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Data.Tenant.Repositories.Impl
{
    public class StoreRepository : RepositoryBase<Store>, IStoreRepository
    {
        private readonly ILogger<OrderRepository> _logger;
        public StoreRepository(
            ITenantDbContextFactory dbContextFactory,
            ICurrentIdentity currentIdentity,
            IIdGenerator idGenerator,
            ILogger<OrderRepository> logger)
            : base( dbContextFactory, currentIdentity, idGenerator)
        {
            _logger = logger;
        }

        public async Task<List<Store>> GetChildDirectStoresAsync(long parentStoreId, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting child direct stores for parentStoreId: {ParentStoreId}", parentStoreId);
            return await AsReadonlyQueryable()
                 .Where(s => s.ParentStoreId == parentStoreId && s.Type == StoreType.DirectOperated)
                 .ToListAsync(ct);
        }

        public async Task<List<long>> GetChildStoreIdsAsync(long parentStoreId, CancellationToken ct = default)
        {
            _logger.LogInformation(_logger.IsEnabled(LogLevel.Debug)
                ? "Getting child store IDs for parentStoreId: {ParentStoreId}"
                : "Getting child store IDs.");
            return await AsReadonlyQueryable()
              .Where(s => s.ParentStoreId == parentStoreId)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        public async Task<List<Store>> GetSiblingDirectStoresAsync(long parentStoreId, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting sibling direct stores for parentStoreId: {ParentStoreId}", parentStoreId);
            return await AsReadonlyQueryable()
                 .Where(s => s.ParentStoreId == parentStoreId && s.Type == StoreType.DirectOperated)
                .ToListAsync(ct);
        }
    }
}