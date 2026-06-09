namespace Atlas.Data.Tenant.Providers;

/// <summary>
/// Resolves tenant database connection metadata by tenant id.
/// </summary>
public interface ITenantConnectionDirectory
{
    Task<TenantConnectionInfo> GetConnectionInfoAsync(
        long tenantId,
        CancellationToken cancellationToken = default);
}
