using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

public sealed class CrawlChannel : BidOpsTenantEntity
{
    public long SourceId { get; set; }

    public string Code { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NoticeType { get; set; } = "TenderAnnouncement";

    public string ListUrl { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string Industry { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;

    public string ScheduleMode { get; set; } = BidOpsCrawlScheduleModes.Interval;

    public int? ScanIntervalMinutes { get; set; }

    public string DailyScanTime { get; set; } = string.Empty;

    public string ListItemSelector { get; set; } = string.Empty;

    public string TitleSelector { get; set; } = string.Empty;

    public string UrlSelector { get; set; } = string.Empty;

    public string PublishTimeSelector { get; set; } = string.Empty;

    public string DetailContentSelector { get; set; } = string.Empty;

    public string AttachmentSelector { get; set; } = string.Empty;

    public DateTime? LastScanTime { get; set; }

    public DateTime? LastSuccessTime { get; set; }

    public string LastError { get; set; } = string.Empty;
}
