using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

public sealed class ReviewQualityIssue : BidOpsTenantEntity
{
    public long ReviewTaskId { get; set; }

    public long RawNoticeId { get; set; }

    public long NoticeStagingId { get; set; }

    public long? PackageStagingId { get; set; }

    public long? OutcomeSupplierRecordId { get; set; }

    public long? ProcurementDetailStagingId { get; set; }

    public string IssueType { get; set; } = string.Empty;

    public ReviewQualityRiskLevel Severity { get; set; } = ReviewQualityRiskLevel.Medium;

    public string FieldName { get; set; } = string.Empty;

    public string Message { get; set; } = string.Empty;

    public string EvidenceJson { get; set; } = string.Empty;

    public bool IsResolved { get; set; }

    public long? ResolvedBy { get; set; }

    public DateTime? ResolvedAt { get; set; }
}
