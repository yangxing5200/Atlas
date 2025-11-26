using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Data.Tenant.Repositories.Impl
{
    public class StoreRepository : RepositoryBase<Store>, IStoreRepository
    {
        private readonly ILogger<StoreRepository> _logger;
        public StoreRepository(
            ITenantDbContextFactory dbContextFactory,
            IDataScope dataScope,
            ILogger<StoreRepository> logger)
            : base(dbContextFactory, dataScope)
        {
            _logger = logger;
        }

        public async Task<List<Store>> GetChildDirectStoresAsync(long parentStoreId, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting child direct stores for parentStoreId: {ParentStoreId}", parentStoreId);
            var builder = await QueryBuilderAsync();
            return await builder.Where(s => s.ParentStoreId == parentStoreId && s.Type == StoreType.DirectOperated)
              .ToListAsync(ct);
        }

        public async Task<List<long>> GetChildStoreIdsAsync(long parentStoreId, CancellationToken ct = default)
        {
            _logger.LogInformation(_logger.IsEnabled(LogLevel.Debug)
                ? "Getting child store IDs for parentStoreId: {ParentStoreId}"
                : "Getting child store IDs.");
            var builder = await QueryBuilderAsync();
            return await builder.Where(s => s.ParentStoreId == parentStoreId).SelectToListAsync(x => x.Id, ct);
        }

        public async Task<List<Store>> GetSiblingDirectStoresAsync(long parentStoreId, CancellationToken ct = default)
        {
            _logger.LogInformation("Getting sibling direct stores for parentStoreId: {ParentStoreId}", parentStoreId);
            var builder = await QueryBuilderAsync();
            return await builder.Where(s => s.ParentStoreId == parentStoreId && s.Type == StoreType.DirectOperated)
                .ToListAsync(ct);
        }
    }
}