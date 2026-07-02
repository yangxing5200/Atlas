using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Outcomes;

/// <summary>
/// 金额候选证据记录。
/// </summary>
public sealed class AmountCandidate : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的闭环包件链接主键。
    /// </summary>
    public long? LifecyclePackageLinkId { get; set; }

    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long RawNoticeId { get; set; }

    /// <summary>
    /// 结果/中标公告原始记录主键。
    /// </summary>
    public long? ResultRawNoticeId { get; set; }

    /// <summary>
    /// 关联的原始附件主键。
    /// </summary>
    public long? RawAttachmentId { get; set; }

    /// <summary>
    /// 关联的结果供应商记录主键。
    /// </summary>
    public long? OutcomeSupplierRecordId { get; set; }

    /// <summary>
    /// 关联的采购明细暂存记录主键。
    /// </summary>
    public long? ProcurementDetailStagingId { get; set; }

    /// <summary>
    /// 关联的正式包件主键。
    /// </summary>
    public long? TenderPackageId { get; set; }

    /// <summary>
    /// 来源类型，用于区分候选数据来自结果行、明细行、公告正文或附件。
    /// </summary>
    public string SourceKind { get; set; } = string.Empty;

    /// <summary>
    /// 来源公告类型。
    /// </summary>
    public string SourceNoticeType { get; set; } = string.Empty;

    /// <summary>
    /// 来源公告或附件标题快照。
    /// </summary>
    public string SourceTitle { get; set; } = string.Empty;

    /// <summary>
    /// 来源附件文件名。
    /// </summary>
    public string SourceFileName { get; set; } = string.Empty;

    /// <summary>
    /// 来源位置，例如页码、表格序号、工作表或行号。
    /// </summary>
    public string SourceLocation { get; set; } = string.Empty;

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
    /// 金额候选业务类型。
    /// </summary>
    public string AmountType { get; set; } = BidOpsAmountCandidateTypes.Unknown;

    /// <summary>
    /// 金额原文。
    /// </summary>
    public string AmountRaw { get; set; } = string.Empty;

    /// <summary>
    /// 归一化后的金额或费率数值。
    /// </summary>
    public decimal? AmountValue { get; set; }

    /// <summary>
    /// 金额原文单位。
    /// </summary>
    public string AmountUnit { get; set; } = string.Empty;

    /// <summary>
    /// 币种代码。
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 是否可能作为最终中标/成交金额。
    /// </summary>
    public bool IsPotentialFinalAmount { get; set; }

    /// <summary>
    /// 规则或 AI 对该候选金额的置信度。
    /// </summary>
    public decimal Confidence { get; set; }

    /// <summary>
    /// 记录状态。
    /// </summary>
    public string Status { get; set; } = BidOpsAmountCandidateStatuses.Candidate;

    /// <summary>
    /// 拒绝原因。
    /// </summary>
    public string RejectReason { get; set; } = string.Empty;

    /// <summary>
    /// 支撑该记录的证据文本。
    /// </summary>
    public string EvidenceText { get; set; } = string.Empty;

    /// <summary>
    /// 证据附近的上下文文本。
    /// </summary>
    public string ContextText { get; set; } = string.Empty;

    /// <summary>
    /// 人工确认或调整备注。
    /// </summary>
    public string ManualRemark { get; set; } = string.Empty;

    /// <summary>
    /// 选择金额候选的用户主键。
    /// </summary>
    public long? SelectedBy { get; set; }

    /// <summary>
    /// 金额候选被选为最终金额的时间。
    /// </summary>
    public DateTime? SelectedAt { get; set; }

    /// <summary>
    /// 拒绝金额候选的用户主键。
    /// </summary>
    public long? RejectedBy { get; set; }

    /// <summary>
    /// 金额候选被拒绝的时间。
    /// </summary>
    public DateTime? RejectedAt { get; set; }

    /// <summary>
    /// 来源证据哈希，用于幂等写入和去重。
    /// </summary>
    public string SourceHash { get; set; } = string.Empty;
}

/// <summary>
/// 金额候选来源类型枚举值。
/// </summary>
public static class BidOpsAmountCandidateSourceKinds
{
    /// <summary>
    /// 来自结果公告供应商行。
    /// </summary>
    public const string OutcomeSupplierRecord = nameof(OutcomeSupplierRecord);
    /// <summary>
    /// 来自采购明细暂存行。
    /// </summary>
    public const string ProcurementDetailStaging = nameof(ProcurementDetailStaging);
    /// <summary>
    /// 来自原始公告正文。
    /// </summary>
    public const string RawNoticeText = nameof(RawNoticeText);
    /// <summary>
    /// 来自原始附件抽取文本。
    /// </summary>
    public const string RawAttachmentText = nameof(RawAttachmentText);
}

/// <summary>
/// 金额候选审核状态枚举值。
/// </summary>
public static class BidOpsAmountCandidateStatuses
{
    /// <summary>
    /// 候选状态或候选建议。
    /// </summary>
    public const string Candidate = "Candidate";
    /// <summary>
    /// 系统推荐候选金额。
    /// </summary>
    public const string Recommended = "Recommended";
    /// <summary>
    /// 已被人工选定为最终金额。
    /// </summary>
    public const string Selected = "Selected";
    /// <summary>
    /// 已被拒绝。
    /// </summary>
    public const string Rejected = "Rejected";
    /// <summary>
    /// 暂无法判断。
    /// </summary>
    public const string Unresolved = "Unresolved";
}

/// <summary>
/// 金额候选业务类型枚举值。
/// </summary>
public static class BidOpsAmountCandidateTypes
{
    /// <summary>
    /// 中标金额，通常可作为最终金额候选。
    /// </summary>
    public const string WinningAmount = "winning_amount";
    /// <summary>
    /// 成交金额，通常可作为最终金额候选。
    /// </summary>
    public const string DealAmount = "deal_amount";
    /// <summary>
    /// 中标价，通常可作为最终价格候选。
    /// </summary>
    public const string WinningPrice = "winning_price";
    /// <summary>
    /// 成交价，通常可作为最终价格候选。
    /// </summary>
    public const string DealPrice = "deal_price";
    /// <summary>
    /// 报价金额。
    /// </summary>
    public const string QuoteAmount = "quote_amount";
    /// <summary>
    /// 投标报价。
    /// </summary>
    public const string BidQuote = "bid_quote";
    /// <summary>
    /// 应答报价。
    /// </summary>
    public const string ResponseQuote = "response_quote";
    /// <summary>
    /// 最终报价。
    /// </summary>
    public const string FinalQuote = "final_quote";
    /// <summary>
    /// 总报价。
    /// </summary>
    public const string TotalQuote = "total_quote";
    /// <summary>
    /// 预算金额，保留为证据但默认不作为最终中标金额。
    /// </summary>
    public const string BudgetAmount = "budget_amount";
    /// <summary>
    /// 最高限价，保留为证据但默认不作为最终中标金额。
    /// </summary>
    public const string CeilingPrice = "ceiling_price";
    /// <summary>
    /// 代理服务费，保留为费用证据。
    /// </summary>
    public const string AgencyFee = "agency_fee";
    /// <summary>
    /// 保证金，保留为费用证据。
    /// </summary>
    public const string Deposit = "deposit";
    /// <summary>
    /// 单价。
    /// </summary>
    public const string UnitPrice = "unit_price";
    /// <summary>
    /// 普通费率。
    /// </summary>
    public const string Rate = "rate";
    /// <summary>
    /// 折扣率。
    /// </summary>
    public const string DiscountRate = "discount_rate";
    /// <summary>
    /// 下浮率。
    /// </summary>
    public const string ReductionRate = "reduction_rate";
    /// <summary>
    /// 暂无法识别金额类型。
    /// </summary>
    public const string Unknown = "unknown";
}
