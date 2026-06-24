using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsCrawlService
{
    Task<CrawlSourceDto> CreateSourceAsync(CreateCrawlSourceRequest request, CancellationToken ct = default);

    Task UpdateSourceAsync(long id, UpdateCrawlSourceRequest request, CancellationToken ct = default);

    Task SetSourceEnabledAsync(long id, bool enabled, string? reason = null, CancellationToken ct = default);

    Task<CrawlChannelDto> CreateChannelAsync(CreateCrawlChannelRequest request, CancellationToken ct = default);

    Task UpdateChannelAsync(long id, UpdateCrawlChannelRequest request, CancellationToken ct = default);

    Task SetChannelEnabledAsync(long id, bool enabled, string? reason = null, CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueMockScanAsync(long channelId, CancellationToken ct = default);

    Task<EnqueueJobDto> StartBackfillAsync(long channelId, StartCrawlBackfillRequest request, CancellationToken ct = default);

    Task<EnqueueJobDto> ContinueCheckpointAsync(long channelId, ContinueCrawlCheckpointRequest request, CancellationToken ct = default);

    Task PauseCheckpointAsync(long channelId, PauseCrawlCheckpointRequest request, CancellationToken ct = default);

    Task<EnqueueJobDto> ResumeCheckpointAsync(long channelId, ContinueCrawlCheckpointRequest request, CancellationToken ct = default);

    Task ResetCheckpointAsync(long channelId, ResetCrawlCheckpointRequest request, CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueManualUrlImportAsync(ImportPublicUrlRequest request, CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueRawAttachmentBackfillAsync(
        BackfillRawNoticeAttachmentsRequest request,
        CancellationToken ct = default);
}
