using Atlas.Core.Entities.Global;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Global.Repositories.Impl;

public sealed class TenantSchemaMigrationStateRepository
    : GlobalRepositoryBase<TenantSchemaMigrationState>, ITenantSchemaMigrationStateRepository
{
    public TenantSchemaMigrationStateRepository(AtlasGlobalDbContext context)
        : base(context)
    {
    }

    public Task<TenantSchemaMigrationState?> GetByTenantIdAsync(
        long tenantId,
        CancellationToken ct = default)
    {
        return _dbSet.FirstOrDefaultAsync(x => x.TenantId == tenantId, ct);
    }

    public async Task<IReadOnlyList<TenantSchemaMigrationState>> ListByTenantIdsAsync(
        IReadOnlyCollection<long> tenantIds,
        CancellationToken ct = default)
    {
        if (tenantIds.Count == 0)
            return Array.Empty<TenantSchemaMigrationState>();

        return await _dbSet
            .AsNoTracking()
            .Where(x => tenantIds.Contains(x.TenantId))
            .ToListAsync(ct);
    }
}
