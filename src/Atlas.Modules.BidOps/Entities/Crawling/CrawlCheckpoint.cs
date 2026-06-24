using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

public sealed class CrawlCheckpoint : BidOpsTenantEntity
{
    public long SourceId { get; set; }

    public long ChannelId { get; set; }

    public string Mode { get; set; } = BidOpsCrawlModes.Incremental;

    public string Status { get; set; } = BidOpsCrawlCheckpointStatuses.Idle;

    public string CursorKind { get; set; } = BidOpsCrawlCursorKinds.PageIndex;

    public string NextCursor { get; set; } = "1";

    public string LastSuccessfulCursor { get; set; } = string.Empty;

    public DateTime? RangeStartPublishTime { get; set; }

    public DateTime? RangeEndPublishTime { get; set; }

    public DateTime? HighWatermarkPublishTime { get; set; }

    public DateTime? LowWatermarkPublishTime { get; set; }

    public int? TotalRemoteCount { get; set; }

    public int ScannedItemCount { get; set; }

    public int CreatedCount { get; set; }

    public int ChangedCount { get; set; }

    public int DuplicateCount { get; set; }

    public int FailedItemCount { get; set; }

    public int? RemainingEstimate { get; set; }

    public DateTime? StartedAt { get; set; }

    public DateTime? LastRunAt { get; set; }

    public DateTime? CompletedAt { get; set; }

    public DateTime? PausedAt { get; set; }

    public string PauseReason { get; set; } = string.Empty;

    public string LastError { get; set; } = string.Empty;
}
