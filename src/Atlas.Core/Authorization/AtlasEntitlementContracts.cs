namespace Atlas.Core.Authorization;

public sealed record EntitlementCheckContext(
    long TenantId,
    long? StoreId,
    DateTime? UtcNow = null);

public interface IEntitlementService
{
    Task<IReadOnlySet<string>> GetAvailableCapabilitiesAsync(
        EntitlementCheckContext context,
        CancellationToken ct = default);

    Task<IReadOnlySet<string>> GetAvailablePermissionsAsync(
        EntitlementCheckContext context,
        CancellationToken ct = default);

    Task<bool> IsPermissionAvailableAsync(
        EntitlementCheckContext context,
        string permissionCode,
        CancellationToken ct = default);

    Task InvalidateEntitlementsAsync(
        long tenantId,
        long? storeId = null,
        CancellationToken ct = default);
}

public interface IAuthorizationCatalogSyncService
{
    Task SyncAsync(CancellationToken ct = default);
}
