using Atlas.Core.Enums;

namespace Atlas.Services.Tenant.Runtime.Migrations;

public sealed record TenantSchemaMigrationPlanItem(
    long TenantId,
    string TenantName,
    string? CurrentVersion,
    string? TargetVersion,
    IReadOnlyList<string> PendingMigrations);

public sealed record TenantSchemaMigrationOptions(
    bool DryRun = false,
    int TenantBatchSize = 100);

public sealed record TenantSchemaMigrationResult(
    long TenantId,
    TenantSchemaMigrationStatus Status,
    string? CurrentVersion,
    string? TargetVersion,
    string? Error);

public sealed record TenantSchemaMigrationBatchResult(
    bool DryRun,
    IReadOnlyList<TenantSchemaMigrationResult> Results)
{
    public int Succeeded => Results.Count(x => x.Status == TenantSchemaMigrationStatus.Succeeded);
    public int Failed => Results.Count(x => x.Status == TenantSchemaMigrationStatus.Failed);
    public int Skipped => Results.Count(x => x.Status == TenantSchemaMigrationStatus.Skipped);
}
