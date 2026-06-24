namespace Atlas.Modules.BidOps.Crawling;

public sealed record StateGridEcpCrawlResult(
    long SourceId,
    long ChannelId,
    int Discovered,
    int Ingested,
    IReadOnlyCollection<long> RawNoticeIds,
    int Created = 0,
    int Changed = 0,
    int Skipped = 0,
    int Failed = 0,
    int StartPage = 1,
    int EndPage = 1,
    int PageSize = 0,
    int PageCount = 1,
    int? TotalRemoteCount = null,
    int? RemainingEstimate = null,
    string NextCursor = "1",
    DateTime? HighWatermarkPublishTime = null,
    DateTime? LowWatermarkPublishTime = null,
    bool IsCompleted = false);

public sealed record StateGridEcpCrawlRequest(
    long ChannelId,
    string Mode,
    long? CheckpointId = null,
    int? StartPage = null,
    int? PageSize = null,
    int? MaxPages = null,
    DateTime? RangeStartPublishTime = null,
    DateTime? RangeEndPublishTime = null);

public interface IStateGridEcpCrawler
{
    Task<StateGridEcpCrawlResult> CrawlAsync(
        long channelId,
        long? backgroundJobId,
        CancellationToken ct = default);

    Task<StateGridEcpCrawlResult> CrawlAsync(
        StateGridEcpCrawlRequest request,
        long? backgroundJobId,
        CancellationToken ct = default);

    Task<long?> ImportPublicDetailAsync(
        string detailUrl,
        long? sourceId,
        long? channelId,
        string? noticeType,
        long? backgroundJobId,
        bool forceRefresh = false,
        CancellationToken ct = default);
}
