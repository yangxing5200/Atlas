using Atlas.Core.Entities.Global;
using Atlas.Data.Abstractions;

namespace Atlas.Data.Global.Repositories;

public interface ITenantSchemaMigrationStateRepository : IRepository<TenantSchemaMigrationState>
{
    Task<TenantSchemaMigrationState?> GetByTenantIdAsync(long tenantId, CancellationToken ct = default);

    Task<IReadOnlyList<TenantSchemaMigrationState>> ListByTenantIdsAsync(
        IReadOnlyCollection<long> tenantIds,
        CancellationToken ct = default);
}
