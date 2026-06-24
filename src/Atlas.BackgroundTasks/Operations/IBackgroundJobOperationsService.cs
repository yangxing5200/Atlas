using Atlas.Models.Tenant.Responses;

namespace Atlas.BackgroundTasks.Operations;

public interface IBackgroundJobOperationsService
{
    Task<PagedResult<BackgroundJobListItemDto>> SearchAsync(
        BackgroundJobSearchQuery query,
        bool bidOpsOnly = false,
        CancellationToken ct = default);

    Task<PagedResult<BackgroundJobListItemDto>> SearchByIdsAsync(
        IReadOnlyCollection<long> jobIds,
        BackgroundJobSearchQuery query,
        bool bidOpsOnly = false,
        CancellationToken ct = default);

    Task<BackgroundJobSummaryDto> GetSummaryAsync(
        BackgroundJobSearchQuery query,
        bool bidOpsOnly = false,
        CancellationToken ct = default);

    Task<BackgroundJobDetailDto?> GetAsync(
        long id,
        bool bidOpsOnly = false,
        CancellationToken ct = default);

    Task<BackgroundJobRetryResultDto?> RetryAsync(
        long id,
        bool bidOpsOnly = false,
        CancellationToken ct = default);

    Task<BackgroundJobCancelResultDto?> CancelAsync(
        long id,
        BackgroundJobCancelRequest request,
        bool bidOpsOnly = false,
        CancellationToken ct = default);
}
