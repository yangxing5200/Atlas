using Atlas.Core.Enums;

namespace Atlas.Services.Abstractions;

public interface IAuditEventService
{
    Task WriteAsync(AuditEventRequest request, CancellationToken ct = default);
}

public sealed class AuditEventRequest
{
    public long TenantId { get; init; }
    public long? UserId { get; init; }
    public long? StoreId { get; init; }
    public string? SessionId { get; init; }
    public string? TraceId { get; init; }
    public string Category { get; init; } = string.Empty;
    public string Action { get; init; } = string.Empty;
    public AuditEventOutcome Outcome { get; init; } = AuditEventOutcome.Succeeded;
    public string? EntityType { get; init; }
    public long? EntityId { get; init; }
    public string? IpAddress { get; init; }
    public string? UserAgent { get; init; }
    public string? Metadata { get; init; }
    public string? ErrorMessage { get; init; }
}
