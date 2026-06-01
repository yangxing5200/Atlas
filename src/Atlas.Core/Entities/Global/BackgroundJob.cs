using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;
using Atlas.Core.Enums;

namespace Atlas.Core.Entities.Global;

public sealed class BackgroundJob : BaseEntity, ISnowflakeId
{
    public string JobType { get; set; } = string.Empty;
    public string Queue { get; set; } = "default";
    public string JobName { get; set; } = string.Empty;
    public string? DeduplicationKey { get; set; }
    public long? TenantId { get; set; }
    public long? StoreId { get; set; }
    public string Payload { get; set; } = string.Empty;
    public BackgroundJobStatus Status { get; set; } = BackgroundJobStatus.Pending;
    public int Priority { get; set; }
    public DateTime AvailableAtUtc { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? LockedAtUtc { get; set; }
    public string? LockedBy { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public int MaxAttempts { get; set; } = 5;
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
    public string? Result { get; set; }
}
