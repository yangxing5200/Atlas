using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Queries;

public interface IBidOpsQueryService
{
    Task<PagedResult<CrawlSourceDto>> SearchSourcesAsync(BidOpsPagedQuery query, CancellationToken ct = default);

    Task<PagedResult<CrawlChannelDto>> SearchChannelsAsync(BidOpsPagedQuery query, CancellationToken ct = default);

    Task<PagedResult<CrawlRunLogDto>> SearchCrawlRunLogsAsync(CrawlRunLogSearchQuery query, CancellationToken ct = default);

    Task<CrawlRunLogDto?> GetCrawlRunLogAsync(long id, CancellationToken ct = default);

    Task<PagedResult<RawNoticeDto>> SearchRawNoticesAsync(RawNoticeSearchQuery query, CancellationToken ct = default);

    Task<RawNoticeDto?> GetRawNoticeAsync(long id, CancellationToken ct = default);

    Task<RawNoticePipelineDto?> GetRawNoticePipelineAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<RawAttachmentDto>> ListRawAttachmentsAsync(long rawNoticeId, CancellationToken ct = default);

    Task<RawAttachmentTextDto?> GetRawAttachmentTextAsync(long rawNoticeId, long attachmentId, CancellationToken ct = default);

    Task<RawAttachmentFileResult?> OpenRawAttachmentFileAsync(long rawNoticeId, long attachmentId, CancellationToken ct = default);

    Task<PagedResult<ReviewTaskDto>> SearchReviewTasksAsync(ReviewTaskSearchQuery query, CancellationToken ct = default);

    Task<PagedResult<ProcessingFailureDto>> SearchProcessingFailuresAsync(ProcessingFailureSearchQuery query, CancellationToken ct = default);

    Task<ReviewTaskDetailDto?> GetReviewTaskDetailAsync(long id, CancellationToken ct = default);

    Task<PagedResult<NoticeDto>> SearchNoticesAsync(BidOpsPagedQuery query, CancellationToken ct = default);

    Task<PagedResult<TenderPackageDto>> SearchPackagesAsync(PackageSearchQuery query, CancellationToken ct = default);

    Task<TenderPackageDto?> GetPackageAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<PackageTimelineItemDto>> GetPackageTimelineAsync(long id, CancellationToken ct = default);

    Task<IReadOnlyList<RequirementItemDto>> ListRequirementsAsync(long packageId, CancellationToken ct = default);
}
