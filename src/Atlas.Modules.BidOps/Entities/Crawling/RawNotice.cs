using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

public sealed class RawNotice : BidOpsTenantEntity
{
    public long SourceId { get; set; }

    public long? ChannelId { get; set; }

    public string SourceNoticeId { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string DetailUrl { get; set; } = string.Empty;

    public string DetailUrlHash { get; set; } = string.Empty;

    public string NoticeType { get; set; } = "TenderAnnouncement";

    public DateTime? PublishTime { get; set; }

    public DateTime FetchTime { get; set; }

    public string ContentHash { get; set; } = string.Empty;

    public string StorageProvider { get; set; } = BidOpsSystemValues.LocalStorageProvider;

    public string HtmlSnapshotStorageKey { get; set; } = string.Empty;

    public string TextContentStorageKey { get; set; } = string.Empty;

    public string TextPreview { get; set; } = string.Empty;

    public RawNoticeStatus Status { get; set; } = RawNoticeStatus.New;

    public string LastError { get; set; } = string.Empty;
}
