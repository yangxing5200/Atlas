using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

/// <summary>
/// 采购明细 AI/规则解析暂存记录。
/// </summary>
public sealed class ProcurementDetailStaging : BidOpsTenantEntity
{
    /// <summary>
    /// 关联的公告暂存记录主键。
    /// </summary>
    public long NoticeStagingId { get; set; }

    /// <summary>
    /// 关联的包件暂存记录主键。
    /// </summary>
    public long? PackageStagingId { get; set; }

    /// <summary>
    /// 关联的原始公告主键。
    /// </summary>
    public long RawNoticeId { get; set; }

    /// <summary>
    /// 关联的原始附件主键。
    /// </summary>
    public long? RawAttachmentId { get; set; }

    /// <summary>
    /// 来源表格序号。
    /// </summary>
    public int? TableIndex { get; set; }

    /// <summary>
    /// 来源表格行序号。
    /// </summary>
    public int? RowIndex { get; set; }

    /// <summary>
    /// 来源 Excel 工作表名称。
    /// </summary>
    public string SourceSheetName { get; set; } = string.Empty;

    /// <summary>
    /// 项目/采购/招标编号。
    /// </summary>
    public string ProjectCode { get; set; } = string.Empty;

    /// <summary>
    /// 项目名称。
    /// </summary>
    public string ProjectName { get; set; } = string.Empty;

    /// <summary>
    /// 采购申请编号。
    /// </summary>
    public string ProcurementApplicationNo { get; set; } = string.Empty;

    /// <summary>
    /// 采购明细行号。
    /// </summary>
    public string LineItemNo { get; set; } = string.Empty;

    /// <summary>
    /// 物料编码。
    /// </summary>
    public string MaterialCode { get; set; } = string.Empty;

    /// <summary>
    /// 分标或标段序号。
    /// </summary>
    public string LotSequence { get; set; } = string.Empty;

    /// <summary>
    /// 分标、标段或分包编号。
    /// </summary>
    public string LotNo { get; set; } = string.Empty;

    /// <summary>
    /// 分标、标段或分包名称。
    /// </summary>
    public string LotName { get; set; } = string.Empty;

    /// <summary>
    /// ECP 来源中的分标名称原文。
    /// </summary>
    public string EcpLotName { get; set; } = string.Empty;

    /// <summary>
    /// 包号。
    /// </summary>
    public string PackageNo { get; set; } = string.Empty;

    /// <summary>
    /// 包件名称。
    /// </summary>
    public string PackageName { get; set; } = string.Empty;

    /// <summary>
    /// 包件类型。
    /// </summary>
    public string PackageType { get; set; } = string.Empty;

    /// <summary>
    /// 品类或业务类别。
    /// </summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>
    /// 采购方式，例如公开招标、竞争性谈判、询价等。
    /// </summary>
    public string ProcurementMethod { get; set; } = string.Empty;

    /// <summary>
    /// 采购人或招标人名称。
    /// </summary>
    public string BuyerName { get; set; } = string.Empty;

    /// <summary>
    /// 项目单位。
    /// </summary>
    public string ProjectUnit { get; set; } = string.Empty;

    /// <summary>
    /// 建设单位。
    /// </summary>
    public string ConstructionUnit { get; set; } = string.Empty;

    /// <summary>
    /// 采购内容描述。
    /// </summary>
    public string ProcurementContent { get; set; } = string.Empty;

    /// <summary>
    /// 采购范围或服务范围文本。
    /// </summary>
    public string ScopeText { get; set; } = string.Empty;

    /// <summary>
    /// 项目概况文本。
    /// </summary>
    public string ProjectOverview { get; set; } = string.Empty;

    /// <summary>
    /// 项目实施地点。
    /// </summary>
    public string Location { get; set; } = string.Empty;

    /// <summary>
    /// 电压等级。
    /// </summary>
    public string VoltageLevel { get; set; } = string.Empty;

    /// <summary>
    /// 采购金额，按人民币元存储。
    /// </summary>
    public decimal? ProcurementAmount { get; set; }

    /// <summary>
    /// 预算金额，按人民币元存储。
    /// </summary>
    public decimal? BudgetAmount { get; set; }

    /// <summary>
    /// 明细行估算金额，按人民币元存储。
    /// </summary>
    public decimal? ItemEstimatedAmount { get; set; }

    /// <summary>
    /// 包件估算金额，按人民币元存储。
    /// </summary>
    public decimal? PackageEstimatedAmount { get; set; }

    /// <summary>
    /// 最高限价，按人民币元存储。
    /// </summary>
    public decimal? MaxPrice { get; set; }

    /// <summary>
    /// 最高限价折扣或费率百分比。
    /// </summary>
    public decimal? MaxPriceRatePercent { get; set; }

    /// <summary>
    /// 税率百分比。
    /// </summary>
    public decimal? TaxRatePercent { get; set; }

    /// <summary>
    /// 应答或投标保证金金额，按人民币元存储。
    /// </summary>
    public decimal? ResponseGuaranteeAmount { get; set; }

    /// <summary>
    /// 报价方式。
    /// </summary>
    public string QuoteMode { get; set; } = string.Empty;

    /// <summary>
    /// 结算方式。
    /// </summary>
    public string SettlementMode { get; set; } = string.Empty;

    /// <summary>
    /// 计划开始日期。
    /// </summary>
    public DateTime? PlannedStartDate { get; set; }

    /// <summary>
    /// 计划完成日期。
    /// </summary>
    public DateTime? PlannedCompletionDate { get; set; }

    /// <summary>
    /// 服务期限天数。
    /// </summary>
    public int? ServicePeriodDays { get; set; }

    /// <summary>
    /// 服务期限原文。
    /// </summary>
    public string ServicePeriodText { get; set; } = string.Empty;

    /// <summary>
    /// 资质要求文本。
    /// </summary>
    public string QualificationRequirement { get; set; } = string.Empty;

    /// <summary>
    /// 业绩要求文本。
    /// </summary>
    public string PerformanceRequirement { get; set; } = string.Empty;

    /// <summary>
    /// 人员要求文本。
    /// </summary>
    public string PersonnelRequirement { get; set; } = string.Empty;

    /// <summary>
    /// 其他要求文本。
    /// </summary>
    public string OtherRequirement { get; set; } = string.Empty;

    /// <summary>
    /// 是否允许联合体的来源表述。
    /// </summary>
    public string JointVentureAllowed { get; set; } = string.Empty;

    /// <summary>
    /// 是否允许分包的来源表述。
    /// </summary>
    public string SubcontractAllowed { get; set; } = string.Empty;

    /// <summary>
    /// 限授或限中规则。
    /// </summary>
    public string AwardLimit { get; set; } = string.Empty;

    /// <summary>
    /// 技术规范书编号。
    /// </summary>
    public string TechnicalSpecId { get; set; } = string.Empty;

    /// <summary>
    /// 合同模板或合同版本。
    /// </summary>
    public string ContractTemplate { get; set; } = string.Empty;

    /// <summary>
    /// 商务评分权重。
    /// </summary>
    public decimal? BusinessWeight { get; set; }

    /// <summary>
    /// 技术评分权重。
    /// </summary>
    public decimal? TechnicalWeight { get; set; }

    /// <summary>
    /// 价格评分权重。
    /// </summary>
    public decimal? PriceWeight { get; set; }

    /// <summary>
    /// 价格计算方法。
    /// </summary>
    public string PriceCalculationMethod { get; set; } = string.Empty;

    /// <summary>
    /// 价格计算参数。
    /// </summary>
    public string PriceParameter { get; set; } = string.Empty;

    /// <summary>
    /// 来源备注或明细备注。
    /// </summary>
    public string Remarks { get; set; } = string.Empty;

    /// <summary>
    /// 来源表头原文 JSON。
    /// </summary>
    public string OriginalHeaderJson { get; set; } = string.Empty;

    /// <summary>
    /// 来源行原文 JSON。
    /// </summary>
    public string OriginalRowJson { get; set; } = string.Empty;

    /// <summary>
    /// 归一化字段 JSON。
    /// </summary>
    public string NormalizedFieldsJson { get; set; } = string.Empty;

    /// <summary>
    /// AI 解析置信度。
    /// </summary>
    public decimal AiConfidence { get; set; }

    /// <summary>
    /// 人工审核状态。
    /// </summary>
    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
}
