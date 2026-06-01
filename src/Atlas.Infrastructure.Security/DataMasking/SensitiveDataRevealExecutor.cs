using Atlas.Core.Services;
using Atlas.Infrastructure.Security.Permissions;

namespace Atlas.Infrastructure.Security.DataMasking;

public sealed class SensitiveDataRevealExecutor : ISensitiveDataRevealExecutor
{
    private readonly ICurrentIdentity _currentIdentity;
    private readonly IPermissionChecker _permissionChecker;
    private readonly ISensitiveDataAccessAuditService _auditService;

    public SensitiveDataRevealExecutor(
        ICurrentIdentity currentIdentity,
        IPermissionChecker permissionChecker,
        ISensitiveDataAccessAuditService auditService)
    {
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _permissionChecker = permissionChecker ?? throw new ArgumentNullException(nameof(permissionChecker));
        _auditService = auditService ?? throw new ArgumentNullException(nameof(auditService));
    }

    public async Task<TResponse> ExecuteAsync<TResponse>(
        SensitiveDataRevealContext context,
        Func<CancellationToken, Task<TResponse>> revealAction,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(revealAction);

        try
        {
            await ValidateAsync(context, ct);
            var response = await revealAction(ct);
            await _auditService.LogRevealSucceededAsync(context, ct);
            return response;
        }
        catch (Exception ex)
        {
            await _auditService.LogRevealFailedAsync(context, ex.Message, ct);
            throw;
        }
    }

    private async Task ValidateAsync(SensitiveDataRevealContext context, CancellationToken ct)
    {
        if (!_currentIdentity.IsAuthenticated || !_currentIdentity.UserId.HasValue || !_currentIdentity.TenantId.HasValue)
            throw new UnauthorizedAccessException("Authenticated user context is required to reveal sensitive data.");

        if (string.IsNullOrWhiteSpace(context.Module))
            throw new ArgumentException("Reveal module is required.", nameof(context));

        if (string.IsNullOrWhiteSpace(context.EntityType))
            throw new ArgumentException("Reveal entity type is required.", nameof(context));

        if (context.EntityId <= 0)
            throw new ArgumentException("Reveal entity id must be greater than zero.", nameof(context));

        if (context.Fields.Count == 0 || context.Fields.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("At least one reveal field is required.", nameof(context));

        if (string.IsNullOrWhiteSpace(context.Reason))
            throw new ArgumentException("Reveal reason is required.", nameof(context));

        if (string.IsNullOrWhiteSpace(context.RequiredPermission))
            throw new ArgumentException("Reveal permission is required.", nameof(context));

        var allowed = await _permissionChecker.HasPermissionAsync(
            new PermissionCheckContext(
                _currentIdentity.TenantId.Value,
                _currentIdentity.UserId.Value,
                _currentIdentity.StoreId,
                context.RequiredPermission),
            ct);

        if (!allowed)
            throw new UnauthorizedAccessException("Current user is not allowed to reveal sensitive data.");
    }
}
