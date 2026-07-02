using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Outcomes;

/// <summary>
/// 结果公告到前置公告/包件的闭环链接。
/// </summary>
public sealed class LifecyclePackageLink : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的正式采购明细主键。
    /// </summary>
    public long? ProcurementDetailId { get; set; }

    /// <summary>
    /// 关联的采购明细暂存记录主键。
    /// </summary>
    public long? ProcurementDetailStagingId { get; set; }

    /// <summary>
    /// 关联的正式包件主键。
    /// </summary>
    public long? TenderPackageId { get; set; }

    /// <summary>
    /// 关联的候选人公示供应商记录主键。
    /// </summary>
    public long? CandidateOutcomeRecordId { get; set; }

    /// <summary>
    /// 关联的中标/成交结果供应商记录主键。
    /// </summary>
    public long? AwardOutcomeRecordId { get; set; }

    /// <summary>
    /// 匹配到的前置公告原始记录主键，历史字段名保留为 Procurement。
    /// </summary>
    public long? ProcurementRawNoticeId { get; set; }

    /// <summary>
    /// 关联的中标候选人公示原始记录主键。
    /// </summary>
    public long? CandidateRawNoticeId { get; set; }

    /// <summary>
    /// 关联的中标/成交结果公告原始记录主键。
    /// </summary>
    public long? AwardRawNoticeId { get; set; }

    /// <summary>
    /// 项目/采购/招标编号。
    /// </summary>
    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>
    /// 项目名称。
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 分标、标段或分包编号。
    /// </summary>
    public string LotNo { get; set; } = string.Empty;

    /// <summary>
    /// 分标、标段或分包名称。
    /// </summary>
    public string LotName { get; set; } = string.Empty;

    /// <summary>
    /// 包号。
    /// </summary>
    public string PackageNo { get; set; } = string.Empty;

    /// <summary>
    /// 包件名称。
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// 供应商名称。
    /// </summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>
    /// 用于匹配的供应商归一化名称。
    /// </summary>
    public string SupplierNameNormalized { get; set; } = string.Empty;

    /// <summary>
    /// 闭环确认的最终中标/成交金额，按人民币元存储。
    /// </summary>
    public decimal? FinalAwardAmount { get; set; }

    /// <summary>
    /// 最终金额来源说明。
    /// </summary>
    public string FinalAwardAmountSource { get; set; } = string.Empty;

    /// <summary>
    /// 币种代码。
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 闭环或匹配得分。
    /// </summary>
    public decimal MatchScore { get; set; }

    /// <summary>
    /// 闭环链接匹配方式。
    /// </summary>
    public string MatchType { get; set; } = BidOpsLifecycleLinkMatchTypes.Suggested;

    /// <summary>
    /// 闭环链接确认状态。
    /// </summary>
    public string LinkStatus { get; set; } = BidOpsLifecycleLinkStatuses.Suggested;

    /// <summary>
    /// 是否需要人工复核。
    /// </summary>
    public bool RequiresManualReview { get; set; } = true;

    /// <summary>
    /// 匹配原因 JSON。
    /// </summary>
    public string MatchReasonsJson { get; set; } = string.Empty;

    /// <summary>
    /// 缺失字段 JSON。
    /// </summary>
    public string MissingFieldsJson { get; set; } = string.Empty;

    /// <summary>
    /// 结构化证据 JSON。
    /// </summary>
    public string EvidenceJson { get; set; } = string.Empty;

    /// <summary>
    /// 人工确认或调整备注。
    /// </summary>
    public string ManualRemark { get; set; } = string.Empty;

    /// <summary>
    /// 确认闭环链接的用户主键。
    /// </summary>
    public long? ConfirmedBy { get; set; }

    /// <summary>
    /// 闭环链接确认时间。
    /// </summary>
    public DateTime? ConfirmedAt { get; set; }

    /// <summary>
    /// 来源证据哈希，用于幂等写入和去重。
    /// </summary>
    public string SourceHash { get; set; } = string.Empty;
}

/// <summary>
/// 闭环链接确认状态枚举值。
/// </summary>
public static class BidOpsLifecycleLinkStatuses
{
    /// <summary>
    /// 系统建议的闭环链接。
    /// </summary>
    public const string Suggested = "Suggested";
    /// <summary>
    /// 人工确认的闭环链接。
    /// </summary>
    public const string Confirmed = "Confirmed";
    /// <summary>
    /// 人工拒绝的闭环链接。
    /// </summary>
    public const string Rejected = "Rejected";
    /// <summary>
    /// 仅用于展示公开结果状态，不参与闭环确认或后续流程。
    /// </summary>
    public const string StatusOnly = "StatusOnly";
}

/// <summary>
/// 闭环链接匹配方式枚举值。
/// </summary>
public static class BidOpsLifecycleLinkMatchTypes
{
    /// <summary>
    /// 普通系统建议匹配。
    /// </summary>
    public const string Suggested = "Suggested";
    /// <summary>
    /// 人工手动匹配。
    /// </summary>
    public const string Manual = "Manual";
    /// <summary>
    /// 规则或证据强匹配。
    /// </summary>
    public const string Strong = "Strong";
    /// <summary>
    /// 弱匹配，需要人工复核。
    /// </summary>
    public const string Weak = "Weak";
    /// <summary>
    /// 公开结果状态展示行，不代表真实闭环匹配。
    /// </summary>
    public const string StatusOnly = "StatusOnly";
}
