using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Crawling;

/// <summary>
/// 采集栏目配置。
/// </summary>
public sealed class CrawlChannel : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的采集来源主键。
    /// </summary>
    public long SourceId { get; set; }

    /// <summary>
    /// 业务编码，通常用于租户内唯一识别。
    /// </summary>
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 显示名称。
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 公告类型，例如招标公告、前置公告、候选人公示或结果公告。
    /// </summary>
    public string NoticeType { get; set; } = "TenderAnnouncement";

    /// <summary>
    /// 公告列表页地址。
    /// </summary>
    public string ListUrl { get; set; } = string.Empty;

    /// <summary>
    /// 地区或属地信息。
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// 行业分类。
    /// </summary>
    public string Industry { get; set; } = string.Empty;

    /// <summary>
    /// 是否启用。
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// 采集计划模式。
    /// </summary>
    public string ScheduleMode { get; set; } = BidOpsCrawlScheduleModes.Interval;

    /// <summary>
    /// 定时扫描间隔，单位分钟。
    /// </summary>
    public int? ScanIntervalMinutes { get; set; }

    /// <summary>
    /// 每日定时扫描时间。
    /// </summary>
    public string DailyScanTime { get; set; } = string.Empty;

    /// <summary>
    /// 列表项 CSS 选择器。
    /// </summary>
    public string ListItemSelector { get; set; } = string.Empty;

    /// <summary>
    /// 标题 CSS 选择器。
    /// </summary>
    public string TitleSelector { get; set; } = string.Empty;

    /// <summary>
    /// 详情链接 CSS 选择器。
    /// </summary>
    public string UrlSelector { get; set; } = string.Empty;

    /// <summary>
    /// 发布时间 CSS 选择器。
    /// </summary>
    public string PublishTimeSelector { get; set; } = string.Empty;

    /// <summary>
    /// 详情正文 CSS 选择器。
    /// </summary>
    public string DetailContentSelector { get; set; } = string.Empty;

    /// <summary>
    /// 附件链接 CSS 选择器。
    /// </summary>
    public string AttachmentSelector { get; set; } = string.Empty;

    /// <summary>
    /// 最近一次扫描时间。
    /// </summary>
    public DateTime? LastScanTime { get; set; }

    /// <summary>
    /// 最近一次成功扫描时间。
    /// </summary>
    public DateTime? LastSuccessTime { get; set; }

    /// <summary>
    /// 最近一次错误信息。
    /// </summary>
    public string LastError { get; set; } = string.Empty;
}
