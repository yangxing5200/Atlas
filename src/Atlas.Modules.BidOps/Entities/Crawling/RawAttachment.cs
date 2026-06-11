using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

public sealed class RawAttachment : BidOpsTenantEntity
{
    public long RawNoticeId { get; set; }

    public string FileName { get; set; } = string.Empty;

    public string FileUrl { get; set; } = string.Empty;

    public string FileType { get; set; } = string.Empty;

    public long? FileSize { get; set; }

    public string FileHash { get; set; } = string.Empty;

    public string StorageProvider { get; set; } = BidOpsSystemValues.LocalStorageProvider;

    public string StorageKey { get; set; } = string.Empty;

    public DownloadStatus DownloadStatus { get; set; } = DownloadStatus.Pending;

    public TextExtractStatus TextExtractStatus { get; set; } = TextExtractStatus.Pending;

    public string TextContentStorageKey { get; set; } = string.Empty;
}
