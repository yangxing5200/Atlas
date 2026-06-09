using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Models.Requests;
using Atlas.Services.Abstractions;

namespace Atlas.Services;

public interface IUserSecurityAuditWriter
{
    Task WriteAsync(
        long tenantId,
        long? userId,
        long? storeId,
        string action,
        AuditEventOutcome outcome,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        string? metadata = null,
        string? sessionId = null);
}

public sealed class UserSecurityAuditWriter : IUserSecurityAuditWriter
{
    private readonly IAuditEventService _auditEventService;

    public UserSecurityAuditWriter(IAuditEventService auditEventService)
    {
        _auditEventService = auditEventService;
    }

    public Task WriteAsync(
        long tenantId,
        long? userId,
        long? storeId,
        string action,
        AuditEventOutcome outcome,
        string? ipAddress = null,
        string? userAgent = null,
        string? errorMessage = null,
        string? metadata = null,
        string? sessionId = null)
    {
        if (tenantId <= 0)
        {
            return Task.CompletedTask;
        }

        return _auditEventService.WriteAsync(new AuditEventRequest
        {
            TenantId = tenantId,
            UserId = userId,
            StoreId = storeId,
            SessionId = sessionId,
            Category = "Security",
            Action = action,
            Outcome = outcome,
            EntityType = userId.HasValue ? nameof(User) : null,
            EntityId = userId,
            IpAddress = ipAddress,
            UserAgent = userAgent,
            Metadata = metadata,
            ErrorMessage = errorMessage
        });
    }
}
