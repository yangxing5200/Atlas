using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Queries;

public interface IBidOpsQueryService
{
    Task<PagedResult<CrawlSourceDto>> SearchSourcesAsync(BidOpsPagedQuery query, CancellationToken ct = default);

    Task<PagedResult<CrawlChannelDto>> SearchChannelsAsync(BidOpsPagedQuery query, CancellationToken ct = default);

    Task<PagedResult<RawNoticeDto>> SearchRawNoticesAsync(RawNoticeSearchQuery query, CancellationToken ct = default);

    Task<RawNoticeDto?> GetRawNoticeAsync(long id, CancellationToken ct = default);

    Task<PagedResult<ReviewTaskDto>> SearchReviewTasksAsync(ReviewTaskSearchQuery query, CancellationToken ct = default);

    Task<ReviewTaskDetailDto?> GetReviewTaskDetailAsync(long id, CancellationToken ct = default);

    Task<PagedResult<NoticeDto>> SearchNoticesAsync(BidOpsPagedQuery query, CancellationToken ct = default);

    Task<PagedResult<TenderPackageDto>> SearchPackagesAsync(PackageSearchQuery query, CancellationToken ct = default);

    Task<IReadOnlyList<RequirementItemDto>> ListRequirementsAsync(long packageId, CancellationToken ct = default);
}
