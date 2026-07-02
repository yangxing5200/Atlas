using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Tendering;

/// <summary>
/// 人工审核后的正式公告。
/// </summary>
public sealed class Notice : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long RawNoticeId { get; set; }

    /// <summary>
    /// 关联的公告暂存记录主键。
    /// </summary>
    public long NoticeStagingId { get; set; }

    /// <summary>
    /// 业务标题。
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 公告类型，例如招标公告、前置公告、候选人公示或结果公告。
    /// </summary>
    public string NoticeType { get; set; } = "TenderAnnouncement";

    /// <summary>
    /// 项目名称。
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 项目/采购/招标编号。
    /// </summary>
    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>
    /// 采购人或招标人名称。
    /// </summary>
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>
    /// 招标代理机构名称。
    /// </summary>
    public string AgencyName { get; set; } = string.Empty;

    /// <summary>
    /// 地区或属地信息。
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// 预算金额，按人民币元存储。
    /// </summary>
    public decimal? BudgetAmount { get; set; }

    /// <summary>
    /// 公告发布时间。
    /// </summary>
    public DateTime? PublishTime { get; set; }

    /// <summary>
    /// 报名截止时间。
    /// </summary>
    public DateTime? SignupDeadline { get; set; }

    /// <summary>
    /// 投标或应答截止时间。
    /// </summary>
    public DateTime? BidDeadline { get; set; }

    /// <summary>
    /// 开标时间。
    /// </summary>
    public DateTime? OpenBidTime { get; set; }

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = "Active";
}
