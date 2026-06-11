namespace Atlas.Modules.BidOps.Crawling;

public sealed record StateGridEcpCrawlResult(
    long SourceId,
    long ChannelId,
    int Discovered,
    int Ingested,
    IReadOnlyCollection<long> RawNoticeIds);

public interface IStateGridEcpCrawler
{
    Task<StateGridEcpCrawlResult> CrawlAsync(
        long channelId,
        long? backgroundJobId,
        CancellationToken ct = default);
}
