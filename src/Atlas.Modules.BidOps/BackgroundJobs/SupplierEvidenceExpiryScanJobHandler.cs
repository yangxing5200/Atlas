using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class SupplierEvidenceExpiryScanJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsSupplierMaintenanceService _maintenance;

    public SupplierEvidenceExpiryScanJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsSupplierMaintenanceService maintenance)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _maintenance = maintenance ?? throw new ArgumentNullException(nameof(maintenance));
    }

    public string JobType => BidOpsBackgroundJobTypes.SupplierEvidenceExpiryScan;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<SupplierEvidenceExpiryScanJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);
        var result = await _maintenance.RunEvidenceExpiryScanAsync(payload.MaxItems, payload.WarningDays, ct);
        return BackgroundJobExecutionResult.Success(result.ToJobResult());
    }
}
