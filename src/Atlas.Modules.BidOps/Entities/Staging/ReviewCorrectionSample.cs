namespace Atlas.Modules.BidOps.Entities.Staging;

/// <summary>
/// 审核纠错样本。
/// </summary>
public sealed class ReviewCorrectionSample : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的审核任务主键。
    /// </summary>
    public long ReviewTaskId { get; set; }

    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long RawNoticeId { get; set; }

    /// <summary>
    /// 公告类型，例如招标公告、前置公告、候选人公示或结果公告。
    /// </summary>
    public string NoticeType { get; set; } = string.Empty;

    /// <summary>
    /// 来源类型，用于区分候选数据来自结果行、明细行、公告正文或附件。
    /// </summary>
    public string SourceKind { get; set; } = BidOpsReviewCorrectionSourceKinds.ManualEdit;

    /// <summary>
    /// 被审核或发生质量问题的字段名。
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 审核前的原始值。
    /// </summary>
    public string OriginalValue { get; set; } = string.Empty;

    /// <summary>
    /// 审核后修正值。
    /// </summary>
    public string CorrectedValue { get; set; } = string.Empty;

    /// <summary>
    /// 来源表头原文。
    /// </summary>
    public string OriginalHeader { get; set; } = string.Empty;

    /// <summary>
    /// 来源行原文 JSON。
    /// </summary>
    public string OriginalRowJson { get; set; } = string.Empty;

    /// <summary>
    /// 审核人补充提示词。
    /// </summary>
    public string ReviewerPrompt { get; set; } = string.Empty;

    /// <summary>
    /// 操作或决策原因。
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 创建样本的用户主键。
    /// </summary>
    public long? CreatedBy { get; set; }
}

/// <summary>
/// 审核纠错样本来源枚举值。
/// </summary>
public static class BidOpsReviewCorrectionSourceKinds
{
    /// <summary>
    /// 人工编辑产生的纠错样本。
    /// </summary>
    public const string ManualEdit = "ManualEdit";
    /// <summary>
    /// 批量审核确认产生的纠错样本。
    /// </summary>
    public const string BulkApprove = "BulkApprove";
    /// <summary>
    /// 重解析提示词产生的纠错样本。
    /// </summary>
    public const string ReparsePrompt = "ReparsePrompt";
    /// <summary>
    /// 审核通过时结果抽取产生的纠错样本。
    /// </summary>
    public const string ApprovalOutcomeExtract = "ApprovalOutcomeExtract";
    /// <summary>
    /// 解决质量问题产生的纠错样本。
    /// </summary>
    public const string IssueResolved = "IssueResolved";
}
