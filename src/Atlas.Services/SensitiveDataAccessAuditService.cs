using System.Text.Json;
using Atlas.Core.Services;
using Atlas.Infrastructure.Security.DataMasking;
using Atlas.Models.Requests;
using Atlas.Services.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Atlas.Services;

public sealed class SensitiveDataAccessAuditService : ISensitiveDataAccessAuditService
{
    private const string OperationType = "SensitiveDataReveal";

    private readonly ICurrentIdentity _currentIdentity;
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IOperationLogService _operationLogService;

    public SensitiveDataAccessAuditService(
        ICurrentIdentity currentIdentity,
        IHttpContextAccessor httpContextAccessor,
        IOperationLogService operationLogService)
    {
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        _operationLogService = operationLogService ?? throw new ArgumentNullException(nameof(operationLogService));
    }

    public Task LogRevealSucceededAsync(
        SensitiveDataRevealContext context,
        CancellationToken ct = default)
    {
        return LogAsync(context, true, null);
    }

    public Task LogRevealFailedAsync(
        SensitiveDataRevealContext context,
        string failureReason,
        CancellationToken ct = default)
    {
        return LogAsync(context, false, failureReason);
    }

    private Task LogAsync(
        SensitiveDataRevealContext context,
        bool isSuccess,
        string? failureReason)
    {
        if (!_currentIdentity.TenantId.HasValue)
            return Task.CompletedTask;

        var payload = new
        {
            operation = OperationType,
            entityType = context.EntityType,
            entityId = context.EntityId,
            fields = context.Fields,
            reason = context.Reason,
            ticketNo = context.TicketNo,
            result = isSuccess ? "Success" : "Failed",
            failureReason
        };

        var httpContext = _httpContextAccessor.HttpContext;
        var request = new LogOperationRequest
        {
            TenantId = _currentIdentity.TenantId.Value,
            UserId = _currentIdentity.UserId,
            StoreId = _currentIdentity.StoreId,
            SessionId = _currentIdentity.SessionId,
            Module = string.IsNullOrWhiteSpace(context.Module) ? "SensitiveData" : context.Module,
            OperationType = OperationType,
            Description = $"Reveal sensitive fields for {context.EntityType}",
            EntityId = context.EntityId > 0 ? context.EntityId : null,
            Changes = JsonSerializer.Serialize(payload),
            IpAddress = httpContext?.Connection.RemoteIpAddress?.ToString(),
            IsSuccess = isSuccess,
            ErrorMessage = failureReason
        };

        return _operationLogService.LogOperationAsync(request);
    }
}
