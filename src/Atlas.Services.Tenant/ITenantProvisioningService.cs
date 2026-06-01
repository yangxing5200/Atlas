namespace Atlas.Services.Tenant;

public interface ITenantProvisioningService
{
    Task<TenantProvisioningResult> ProvisionAsync(
        TenantProvisioningRequest request,
        CancellationToken ct = default);
}
