using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

/// <summary>
/// 审核质量问题记录。
/// </summary>
public sealed class ReviewQualityIssue : BidOpsTenantEntity
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
    /// 关联的公告暂存记录主键。
    /// </summary>
    public long NoticeStagingId { get; set; }

    /// <summary>
    /// 关联的包件暂存记录主键。
    /// </summary>
    public long? PackageStagingId { get; set; }

    /// <summary>
    /// 关联的结果供应商记录主键。
    /// </summary>
    public long? OutcomeSupplierRecordId { get; set; }

    /// <summary>
    /// 关联的采购明细暂存记录主键。
    /// </summary>
    public long? ProcurementDetailStagingId { get; set; }

    /// <summary>
    /// 质量问题类型。
    /// </summary>
    public string IssueType { get; set; } = string.Empty;

    /// <summary>
    /// 问题严重程度。
    /// </summary>
    public ReviewQualityRiskLevel Severity { get; set; } = ReviewQualityRiskLevel.Medium;

    /// <summary>
    /// 被审核或发生质量问题的字段名。
    /// </summary>
    public string FieldName { get; set; } = string.Empty;

    /// <summary>
    /// 状态消息或日志内容。
    /// </summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 结构化证据 JSON。
    /// </summary>
    public string EvidenceJson { get; set; } = string.Empty;

    /// <summary>
    /// 该质量问题是否已解决。
    /// </summary>
    public bool IsResolved { get; set; }

    /// <summary>
    /// 解决质量问题的用户主键。
    /// </summary>
    public long? ResolvedBy { get; set; }

    /// <summary>
    /// 质量问题解决时间。
    /// </summary>
    public DateTime? ResolvedAt { get; set; }
}
