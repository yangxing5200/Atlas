using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

public sealed class ProcurementDetailStaging : BidOpsTenantEntity
{
    public long NoticeStagingId { get; set; }

    public long? PackageStagingId { get; set; }

    public long RawNoticeId { get; set; }

    public long? RawAttachmentId { get; set; }

    public int? TableIndex { get; set; }

    public int? RowIndex { get; set; }

    public string SourceSheetName { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ProcurementApplicationNo { get; set; } = string.Empty;

    public string LineItemNo { get; set; } = string.Empty;

    public string MaterialCode { get; set; } = string.Empty;

    public string LotSequence { get; set; } = string.Empty;

    public string LotNo { get; set; } = string.Empty;

    public string LotName { get; set; } = string.Empty;

    public string EcpLotName { get; set; } = string.Empty;

    public string PackageNo { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;

    public string PackageType { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string ProcurementMethod { get; set; } = string.Empty;

    public string BuyerName { get; set; } = string.Empty;

    public string ProjectUnit { get; set; } = string.Empty;

    public string ConstructionUnit { get; set; } = string.Empty;

    public string ProcurementContent { get; set; } = string.Empty;

    public string ScopeText { get; set; } = string.Empty;

    public string ProjectOverview { get; set; } = string.Empty;

    public string Location { get; set; } = string.Empty;

    public string VoltageLevel { get; set; } = string.Empty;

    public decimal? ProcurementAmount { get; set; }

    public decimal? BudgetAmount { get; set; }

    public decimal? ItemEstimatedAmount { get; set; }

    public decimal? PackageEstimatedAmount { get; set; }

    public decimal? MaxPrice { get; set; }

    public decimal? MaxPriceRatePercent { get; set; }

    public decimal? TaxRatePercent { get; set; }

    public decimal? ResponseGuaranteeAmount { get; set; }

    public string QuoteMode { get; set; } = string.Empty;

    public string SettlementMode { get; set; } = string.Empty;

    public DateTime? PlannedStartDate { get; set; }

    public DateTime? PlannedCompletionDate { get; set; }

    public int? ServicePeriodDays { get; set; }

    public string ServicePeriodText { get; set; } = string.Empty;

    public string QualificationRequirement { get; set; } = string.Empty;

    public string PerformanceRequirement { get; set; } = string.Empty;

    public string PersonnelRequirement { get; set; } = string.Empty;

    public string OtherRequirement { get; set; } = string.Empty;

    public string JointVentureAllowed { get; set; } = string.Empty;

    public string SubcontractAllowed { get; set; } = string.Empty;

    public string AwardLimit { get; set; } = string.Empty;

    public string TechnicalSpecId { get; set; } = string.Empty;

    public string ContractTemplate { get; set; } = string.Empty;

    public decimal? BusinessWeight { get; set; }

    public decimal? TechnicalWeight { get; set; }

    public decimal? PriceWeight { get; set; }

    public string PriceCalculationMethod { get; set; } = string.Empty;

    public string PriceParameter { get; set; } = string.Empty;

    public string Remarks { get; set; } = string.Empty;

    public string OriginalHeaderJson { get; set; } = string.Empty;

    public string OriginalRowJson { get; set; } = string.Empty;

    public string NormalizedFieldsJson { get; set; } = string.Empty;

    public decimal AiConfidence { get; set; }

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
}
