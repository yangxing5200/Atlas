using Atlas.Core.Entities.Base;
using Atlas.Core.Enums;

namespace Atlas.Core.Entities.Global;

public sealed class TenantSchemaMigrationState : BaseEntity
{
    public long TenantId { get; set; }
    public string? CurrentVersion { get; set; }
    public string? TargetVersion { get; set; }
    public TenantSchemaMigrationStatus Status { get; set; } = TenantSchemaMigrationStatus.Pending;
    public string? LastError { get; set; }
    public int RetryCount { get; set; }
    public DateTime? LastAttemptedAtUtc { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
}
