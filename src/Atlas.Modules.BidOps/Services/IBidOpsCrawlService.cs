using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Services;

public interface IBidOpsCrawlService
{
    Task<CrawlSourceDto> CreateSourceAsync(CreateCrawlSourceRequest request, CancellationToken ct = default);

    Task UpdateSourceAsync(long id, UpdateCrawlSourceRequest request, CancellationToken ct = default);

    Task SetSourceEnabledAsync(long id, bool enabled, string? reason = null, CancellationToken ct = default);

    Task<CrawlChannelDto> CreateChannelAsync(CreateCrawlChannelRequest request, CancellationToken ct = default);

    Task UpdateChannelAsync(long id, UpdateCrawlChannelRequest request, CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueMockScanAsync(long channelId, CancellationToken ct = default);

    Task<EnqueueJobDto> EnqueueManualUrlImportAsync(ImportPublicUrlRequest request, CancellationToken ct = default);
}
