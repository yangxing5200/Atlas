namespace Atlas.Services.Tenant.Runtime.Migrations;

public interface ITenantSchemaMigrationService
{
    Task<IReadOnlyList<TenantSchemaMigrationPlanItem>> BuildPlanAsync(CancellationToken ct = default);

    Task<TenantSchemaMigrationBatchResult> ExecuteAsync(
        TenantSchemaMigrationOptions options,
        CancellationToken ct = default);
}
