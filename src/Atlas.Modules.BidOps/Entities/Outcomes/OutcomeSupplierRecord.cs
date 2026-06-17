using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Outcomes;

public sealed class OutcomeSupplierRecord : BidOpsTenantEntity
{
    public long RawNoticeId { get; set; }

    public long? NoticeId { get; set; }

    public long? TenderPackageId { get; set; }

    public long? BuyerId { get; set; }

    public long? SupplierId { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string NoticeTitle { get; set; } = string.Empty;

    public string NoticeType { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string BuyerName { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public DateTime? PublishTime { get; set; }

    public string LotNo { get; set; } = string.Empty;

    public string LotName { get; set; } = string.Empty;

    public string PackageNo { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;

    public string Category { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string SupplierNameNormalized { get; set; } = string.Empty;

    public string OutcomeType { get; set; } = BidOpsOutcomeTypes.Candidate;

    public int? Rank { get; set; }

    public decimal? AwardAmount { get; set; }

    public decimal? ProcurementAgencyServiceFeeAmount { get; set; }

    public int ExtractionOrder { get; set; }

    public string Currency { get; set; } = "CNY";

    public string EvidenceText { get; set; } = string.Empty;

    public decimal ExtractionConfidence { get; set; }

    public string SourceHash { get; set; } = string.Empty;
}

public static class BidOpsOutcomeTypes
{
    public const string Awarded = "Awarded";
    public const string Candidate = "Candidate";
    public const string Shortlisted = "Shortlisted";
}
