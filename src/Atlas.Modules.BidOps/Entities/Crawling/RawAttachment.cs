using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

/// <summary>
/// 原始公告附件元数据。
/// </summary>
public sealed class RawAttachment : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long RawNoticeId { get; set; }

    /// <summary>
    /// 附件或文件名。
    /// </summary>
    public string FileName { get; set; } = string.Empty;

    /// <summary>
    /// 附件原始下载地址。
    /// </summary>
    public string FileUrl { get; set; } = string.Empty;

    /// <summary>
    /// 附件文件类型或扩展名。
    /// </summary>
    public string FileType { get; set; } = string.Empty;

    /// <summary>
    /// 附件大小，单位字节。
    /// </summary>
    public long? FileSize { get; set; }

    /// <summary>
    /// 附件文件哈希，用于去重和变更识别。
    /// </summary>
    public string FileHash { get; set; } = string.Empty;

    /// <summary>
    /// 文件存储提供方标识。
    /// </summary>
    public string StorageProvider { get; set; } = BidOpsSystemValues.LocalStorageProvider;

    /// <summary>
    /// 文件在对象存储或本地文件存储中的键。
    /// </summary>
    public string StorageKey { get; set; } = string.Empty;

    /// <summary>
    /// 附件下载状态。
    /// </summary>
    public DownloadStatus DownloadStatus { get; set; } = DownloadStatus.Pending;

    /// <summary>
    /// 文本抽取状态。
    /// </summary>
    public TextExtractStatus TextExtractStatus { get; set; } = TextExtractStatus.Pending;

    /// <summary>
    /// 抽取文本内容的存储键。
    /// </summary>
    public string TextContentStorageKey { get; set; } = string.Empty;
}
