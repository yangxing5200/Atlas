using System.Diagnostics;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Services.Abstractions;
using Microsoft.Extensions.Logging;

namespace Atlas.Services;

public sealed class AuditEventService : IAuditEventService
{
    private readonly IRepository<AuditEvent> _auditEvents;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentIdentity _currentIdentity;
    private readonly ILogger<AuditEventService> _logger;

    public AuditEventService(
        IRepository<AuditEvent> auditEvents,
        IUnitOfWork unitOfWork,
        ICurrentIdentity currentIdentity,
        ILogger<AuditEventService> logger)
    {
        _auditEvents = auditEvents ?? throw new ArgumentNullException(nameof(auditEvents));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task WriteAsync(AuditEventRequest request, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);

        var tenantId = request.TenantId != 0
            ? request.TenantId
            : _currentIdentity.TenantId.GetValueOrDefault();
        if (tenantId <= 0)
        {
            _logger.LogWarning("Audit event skipped because tenant id is missing for {Category}/{Action}.", request.Category, request.Action);
            return;
        }

        try
        {
            var auditEvent = new AuditEvent
            {
                TenantId = tenantId,
                UserId = request.UserId ?? _currentIdentity.UserId,
                StoreId = request.StoreId ?? _currentIdentity.StoreId,
                SessionId = request.SessionId ?? _currentIdentity.SessionId,
                TraceId = request.TraceId ?? Activity.Current?.TraceId.ToString(),
                Category = request.Category,
                Action = request.Action,
                Outcome = request.Outcome,
                EntityType = request.EntityType,
                EntityId = request.EntityId,
                IpAddress = request.IpAddress,
                UserAgent = request.UserAgent,
                Metadata = request.Metadata,
                ErrorMessage = request.ErrorMessage
            };

            await _auditEvents.AddAsync(auditEvent, tenantId, ct);
            await _unitOfWork.SaveChangesAsync(tenantId, ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to write audit event {Category}/{Action}. The primary operation result is not changed.",
                request.Category,
                request.Action);
        }
    }
}
