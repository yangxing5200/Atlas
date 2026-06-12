using Atlas.Models.Tenant.Responses;

namespace Atlas.BackgroundTasks.Operations;

public interface IBackgroundWorkerOperationsService
{
    Task<PagedResult<BackgroundWorkerHeartbeatDto>> SearchAsync(
        BackgroundWorkerSearchQuery query,
        CancellationToken ct = default);
}
