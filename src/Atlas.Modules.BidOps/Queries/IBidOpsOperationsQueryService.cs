using Atlas.BackgroundTasks.Operations;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Queries;

public interface IBidOpsOperationsQueryService
{
    Task<BidOpsOperationsDashboardDto> GetDashboardAsync(CancellationToken ct = default);

    Task<BidOpsDashboardSummaryDto> GetBusinessDashboardAsync(CancellationToken ct = default);

    Task<PagedResult<BackgroundJobListItemDto>> SearchJobsAsync(
        BackgroundJobSearchQuery query,
        CancellationToken ct = default);

    Task<BidOpsConfigCheckDto> GetConfigCheckAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BidOpsChannelHealthDto>> GetChannelHealthAsync(CancellationToken ct = default);

    Task<IReadOnlyList<BidOpsCrawlProgressDto>> GetCrawlProgressAsync(CancellationToken ct = default);
}
