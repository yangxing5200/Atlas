using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

public sealed class CrawlRun : BidOpsTenantEntity
{
    public long SourceId { get; set; }

    public long ChannelId { get; set; }

    public long? CheckpointId { get; set; }

    public long? BackgroundJobId { get; set; }

    public string Mode { get; set; } = BidOpsCrawlModes.Incremental;

    public string Status { get; set; } = BidOpsCrawlRunStatuses.Running;

    public string StartCursor { get; set; } = string.Empty;

    public string EndCursor { get; set; } = string.Empty;

    public int PageSize { get; set; }

    public int PageCount { get; set; }

    public int DiscoveredCount { get; set; }

    public int CreatedCount { get; set; }

    public int ChangedCount { get; set; }

    public int DuplicateCount { get; set; }

    public int FailedItemCount { get; set; }

    public int? TotalRemoteCount { get; set; }

    public int? RemainingEstimate { get; set; }

    public DateTime StartedAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public string Message { get; set; } = string.Empty;
}
