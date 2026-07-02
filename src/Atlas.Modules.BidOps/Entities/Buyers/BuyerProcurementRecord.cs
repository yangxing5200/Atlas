using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Buyers;

/// <summary>
/// 采购人历史采购公告记录。
/// </summary>
public sealed class BuyerProcurementRecord : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的采购人主键。
    /// </summary>
    public long BuyerId { get; set; }

    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long RawNoticeId { get; set; }

    /// <summary>
    /// 关联的正式公告主键。
    /// </summary>
    public long? NoticeId { get; set; }

    /// <summary>
    /// 来源公告或来源页面地址。
    /// </summary>
    public string SourceUrl { get; set; } = string.Empty;

    /// <summary>
    /// 公告标题快照。
    /// </summary>
    public string NoticeTitle { get; set; } = string.Empty;

    /// <summary>
    /// 公告类型，例如招标公告、前置公告、候选人公示或结果公告。
    /// </summary>
    public string NoticeType { get; set; } = string.Empty;

    /// <summary>
    /// 项目名称。
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 项目/采购/招标编号。
    /// </summary>
    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>
    /// 地区或属地信息。
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// 公告发布时间。
    /// </summary>
    public DateTime? PublishTime { get; set; }

    /// <summary>
    /// 预算金额，按人民币元存储。
    /// </summary>
    public decimal? BudgetAmount { get; set; }

    /// <summary>
    /// 公告中识别出的包件数量。
    /// </summary>
    public int PackageCount { get; set; }

    /// <summary>
    /// 来源证据哈希，用于幂等写入和去重。
    /// </summary>
    public string SourceHash { get; set; } = string.Empty;

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;
}
