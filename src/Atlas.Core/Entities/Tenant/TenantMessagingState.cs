using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;

namespace Atlas.Core.Entities.Tenant;

public sealed class TenantOutboxMessage : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }
    public long? StoreId { get; set; }
    public Guid EventId { get; set; }
    public string EventName { get; set; } = string.Empty;
    public string MessageType { get; set; } = string.Empty;
    public string Payload { get; set; } = string.Empty;
    public DateTime OccurredAtUtc { get; set; }
    public DateTime? AvailableAtUtc { get; set; }
    public DateTime? ProcessingAtUtc { get; set; }
    public string? ProcessingBy { get; set; }
    public DateTime? ProcessedAtUtc { get; set; }
    public int AttemptCount { get; set; }
    public DateTime? NextAttemptAtUtc { get; set; }
    public string? LastError { get; set; }
}

public sealed class TenantInboxMessage : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }
    public Guid MessageId { get; set; }
    public string ConsumerName { get; set; } = string.Empty;
    public DateTime ReceivedAtUtc { get; set; }
}
