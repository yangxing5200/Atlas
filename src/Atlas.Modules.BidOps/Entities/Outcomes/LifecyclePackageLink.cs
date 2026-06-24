using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Outcomes;

public sealed class LifecyclePackageLink : BidOpsTenantEntity
{
    public long? ProcurementDetailId { get; set; }

    public long? ProcurementDetailStagingId { get; set; }

    public long? TenderPackageId { get; set; }

    public long? CandidateOutcomeRecordId { get; set; }

    public long? AwardOutcomeRecordId { get; set; }

    public long? ProcurementRawNoticeId { get; set; }

    public long? CandidateRawNoticeId { get; set; }

    public long? AwardRawNoticeId { get; set; }

    public string ProjectCode { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string LotNo { get; set; } = string.Empty;

    public string LotName { get; set; } = string.Empty;

    public string PackageNo { get; set; } = string.Empty;

    public string PackageName { get; set; } = string.Empty;

    public string SupplierName { get; set; } = string.Empty;

    public string SupplierNameNormalized { get; set; } = string.Empty;

    public decimal? FinalAwardAmount { get; set; }

    public string FinalAwardAmountSource { get; set; } = string.Empty;

    public string Currency { get; set; } = "CNY";

    public decimal MatchScore { get; set; }

    public string MatchType { get; set; } = BidOpsLifecycleLinkMatchTypes.Suggested;

    public string LinkStatus { get; set; } = BidOpsLifecycleLinkStatuses.Suggested;

    public bool RequiresManualReview { get; set; } = true;

    public string MatchReasonsJson { get; set; } = string.Empty;

    public string MissingFieldsJson { get; set; } = string.Empty;

    public string EvidenceJson { get; set; } = string.Empty;

    public string ManualRemark { get; set; } = string.Empty;

    public long? ConfirmedBy { get; set; }

    public DateTime? ConfirmedAt { get; set; }

    public string SourceHash { get; set; } = string.Empty;
}

public static class BidOpsLifecycleLinkStatuses
{
    public const string Suggested = "Suggested";
    public const string Confirmed = "Confirmed";
    public const string Rejected = "Rejected";
}

public static class BidOpsLifecycleLinkMatchTypes
{
    public const string Suggested = "Suggested";
    public const string Manual = "Manual";
    public const string Strong = "Strong";
    public const string Weak = "Weak";
}
