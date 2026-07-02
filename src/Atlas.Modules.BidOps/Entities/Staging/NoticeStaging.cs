using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

/// <summary>
/// 公告 AI/规则解析暂存记录。
/// </summary>
public sealed class NoticeStaging : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long RawNoticeId { get; set; }

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
    /// AI 解析置信度。
    /// </summary>
    public decimal AiConfidence { get; set; }

    /// <summary>
    /// 人工审核状态。
    /// </summary>
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;

    /// <summary>
    /// 审核人用户主键。
    /// </summary>
    public long? ReviewerId { get; set; }

    /// <summary>
    /// 审核完成时间。
    /// </summary>
    public DateTime? ReviewedAt { get; set; }

    /// <summary>
    /// AI 原始输出内容的存储键。
    /// </summary>
    public string RawAiOutputStorageKey { get; set; } = string.Empty;
}
