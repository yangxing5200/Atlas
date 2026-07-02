using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Outcomes;

/// <summary>
/// 结果公告供应商行记录。
/// </summary>
public sealed class OutcomeSupplierRecord : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long RawNoticeId { get; set; }

    /// <summary>
    /// 关联的正式公告主键。
    /// </summary>
    public long? NoticeId { get; set; }

    /// <summary>
    /// 关联的正式包件主键。
    /// </summary>
    public long? TenderPackageId { get; set; }

    /// <summary>
    /// 关联的采购人主键。
    /// </summary>
    public long? BuyerId { get; set; }

    /// <summary>
    /// 关联的供应商主键。
    /// </summary>
    public long? SupplierId { get; set; }

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
    /// 采购人或招标人名称。
    /// </summary>
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>
    /// 地区或属地信息。
    /// </summary>
    public string Region { get; set; } = string.Empty;

    /// <summary>
    /// 公告发布时间。
    /// </summary>
    public DateTime? PublishTime { get; set; }

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
    /// 品类或业务类别。
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 供应商名称。
    /// </summary>
    public string SupplierName { get; set; } = string.Empty;

    /// <summary>
    /// 用于匹配的供应商归一化名称。
    /// </summary>
    public string SupplierNameNormalized { get; set; } = string.Empty;

    /// <summary>
    /// 结果类型，例如中标、候选或入围。
    /// </summary>
    public string OutcomeType { get; set; } = BidOpsOutcomeTypes.Candidate;

    /// <summary>
    /// 排序名次或中标候选人排名。
    /// </summary>
    public int? Rank { get; set; }

    /// <summary>
    /// 中标/成交金额，按人民币元存储。
    /// </summary>
    public decimal? AwardAmount { get; set; }

    /// <summary>
    /// 采购代理服务费金额，按人民币元存储。
    /// </summary>
    public decimal? ProcurementAgencyServiceFeeAmount { get; set; }

    /// <summary>
    /// 抽取顺序，用于保持来源表格或文本顺序。
    /// </summary>
    public int ExtractionOrder { get; set; }

    /// <summary>
    /// 币种代码。
    /// </summary>
    public string Currency { get; set; } = "CNY";

    /// <summary>
    /// 支撑该记录的证据文本。
    /// </summary>
    public string EvidenceText { get; set; } = string.Empty;

    /// <summary>
    /// 结果供应商抽取置信度。
    /// </summary>
    public decimal ExtractionConfidence { get; set; }

    /// <summary>
    /// 来源证据哈希，用于幂等写入和去重。
    /// </summary>
    public string SourceHash { get; set; } = string.Empty;
}

/// <summary>
/// 结果公告供应商结果类型枚举值。
/// </summary>
public static class BidOpsOutcomeTypes
{
    /// <summary>
    /// 中标或成交供应商。
    /// </summary>
    public const string Awarded = "Awarded";
    /// <summary>
    /// 中标候选人。
    /// </summary>
    public const string Candidate = "Candidate";
    /// <summary>
    /// 入围供应商。
    /// </summary>
    public const string Shortlisted = "Shortlisted";
    /// <summary>
    /// 流标、废标或采购失败行，仅用于公告结果展示，不作为成交闭环依据。
    /// </summary>
    public const string Failed = "Failed";
}
