using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

/// <summary>
/// 人工审核任务。
/// </summary>
public sealed class ReviewTask : BidOpsTenantEntity
{
    /// <summary>
    /// 被审核业务对象类型。
    /// </summary>
    public string BizType { get; set; } = string.Empty;

    /// <summary>
    /// 被审核业务对象主键。
    /// </summary>
    public long BizId { get; set; }

    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long? RawNoticeId { get; set; }

    /// <summary>
    /// 审核任务标题。
    /// </summary>
    public string TaskTitle { get; set; } = string.Empty;

    /// <summary>
    /// 优先级，数值越小通常越靠前处理。
    /// </summary>
    public int Priority { get; set; }

    /// <summary>
    /// 记录状态。
    /// </summary>
    public ReviewTaskStatus Status { get; set; } = ReviewTaskStatus.Pending;

    /// <summary>
    /// 质量评分。
    /// </summary>
    public int QualityScore { get; set; } = 100;

    /// <summary>
    /// 风险等级。
    /// </summary>
    public ReviewQualityRiskLevel RiskLevel { get; set; } = ReviewQualityRiskLevel.Low;

    /// <summary>
    /// 审核质量问题总数。
    /// </summary>
    public int QualityIssueCount { get; set; }

    /// <summary>
    /// 高风险审核质量问题数量。
    /// </summary>
    public int HighRiskIssueCount { get; set; }

    /// <summary>
    /// 系统给出的审核建议。
    /// </summary>
    public ReviewRecommendation ReviewRecommendation { get; set; } = ReviewRecommendation.BatchConfirmCandidate;

    /// <summary>
    /// 审核任务当前指派用户主键。
    /// </summary>
    public long? AssignedTo { get; set; }

    /// <summary>
    /// 审核人用户主键。
    /// </summary>
    public long? ReviewerId { get; set; }

    /// <summary>
    /// 审核结论。
    /// </summary>
    public string Decision { get; set; } = string.Empty;

    /// <summary>
    /// 人工备注。
    /// </summary>
    public string Remark { get; set; } = string.Empty;

    /// <summary>
    /// 审核完成时间。
    /// </summary>
    public DateTime? ReviewedAt { get; set; }
}
