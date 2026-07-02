using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

/// <summary>
/// 原始公告元数据和快照索引。
/// </summary>
public sealed class RawNotice : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的采集来源主键。
    /// </summary>
    public long SourceId { get; set; }

    /// <summary>
    /// 关联的采集栏目主键。
    /// </summary>
    public long? ChannelId { get; set; }

    /// <summary>
    /// 来源站点公告标识，不等同于项目编号。
    /// </summary>
    public string SourceNoticeId { get; set; } = string.Empty;

    /// <summary>
    /// 业务标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 公告详情页地址。
    /// </summary>
    public string DetailUrl { get; set; } = string.Empty;

    /// <summary>
    /// 详情页地址哈希，用于租户内去重。
    /// </summary>
    public string DetailUrlHash { get; set; } = string.Empty;

    /// <summary>
    /// 公告类型，例如招标公告、前置公告、候选人公示或结果公告。
    /// </summary>
    public string NoticeType { get; set; } = "TenderAnnouncement";

    /// <summary>
    /// 公告发布时间。
    /// </summary>
    public DateTime? PublishTime { get; set; }

    /// <summary>
    /// 公告抓取时间。
    /// </summary>
    public DateTime FetchTime { get; set; }

    /// <summary>
    /// 公告正文内容哈希，用于变更识别。
    /// </summary>
    public string ContentHash { get; set; } = string.Empty;

    /// <summary>
    /// 文件存储提供方标识。
    /// </summary>
    public string StorageProvider { get; set; } = BidOpsSystemValues.LocalStorageProvider;

    /// <summary>
    /// HTML 原文快照的存储键。
    /// </summary>
    public string HtmlSnapshotStorageKey { get; set; } = string.Empty;

    /// <summary>
    /// 抽取文本内容的存储键。
    /// </summary>
    public string TextContentStorageKey { get; set; } = string.Empty;

    /// <summary>
    /// 公告文本预览，用于列表检索和人工快速判断。
    /// </summary>
    public string TextPreview { get; set; } = string.Empty;

    /// <summary>
    /// 记录状态。
    /// </summary>
    public RawNoticeStatus Status { get; set; } = RawNoticeStatus.New;

    /// <summary>
    /// 最近一次错误信息。
    /// </summary>
    public string LastError { get; set; } = string.Empty;
}
