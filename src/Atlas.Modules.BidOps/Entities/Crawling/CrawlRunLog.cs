using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

public sealed class CrawlRunLog : BidOpsTenantEntity
{
    public long? SourceId { get; set; }

    public long? ChannelId { get; set; }

    public long? BackgroundJobId { get; set; }

    public string Operation { get; set; } = string.Empty;

    public string Status { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public int? DurationMs { get; set; }
}
