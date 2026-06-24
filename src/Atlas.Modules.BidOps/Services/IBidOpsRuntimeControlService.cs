using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsRuntimeControlService
{
    Task<BidOpsRuntimeStatusDto> GetStatusAsync(CancellationToken ct = default);

    Task<BidOpsRuntimeStatusDto> SetTaskPauseAsync(
        UpdateBidOpsTaskPauseRequest request,
        CancellationToken ct = default);

    Task<bool> IsTaskPausedAsync(long tenantId, CancellationToken ct = default);

    Task EnsureTasksNotPausedAsync(CancellationToken ct = default);
}
