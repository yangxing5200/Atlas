using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Services;

namespace Atlas.Modules.BidOps.BackgroundJobs;

public sealed class SupplierMatchRunJobHandler : IBackgroundJobHandler
{
    private readonly IExecutionIdentityAccessor _identityAccessor;
    private readonly IBidOpsMatchingService _matching;

    public SupplierMatchRunJobHandler(
        IExecutionIdentityAccessor identityAccessor,
        IBidOpsMatchingService matching)
    {
        _identityAccessor = identityAccessor ?? throw new ArgumentNullException(nameof(identityAccessor));
        _matching = matching ?? throw new ArgumentNullException(nameof(matching));
    }

    public string JobType => BidOpsBackgroundJobTypes.SupplierMatchRun;

    public async Task<BackgroundJobExecutionResult> HandleAsync(
        BackgroundJobExecutionContext context,
        CancellationToken ct = default)
    {
        var payload = context.GetPayload<SupplierMatchRunJobPayload>();
        using var identity = BidOpsJobIdentity.Begin(_identityAccessor, payload);
        var result = await _matching.ExecuteSupplierMatchRunAsync(payload.RunId, payload.MaxSuppliers, ct);
        return BackgroundJobExecutionResult.Success(result.ToJobResult());
    }
}
