using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

public sealed class RequirementStaging : BidOpsTenantEntity
{
    public long PackageStagingId { get; set; }

    public string RequirementType { get; set; } = string.Empty;

    public string OriginalText { get; set; } = string.Empty;

    public long? SourceFileId { get; set; }

    public int? SourcePage { get; set; }

    public bool IsMandatory { get; set; }

    public bool IsRejectRisk { get; set; }

    public string RequiredEvidenceType { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "Medium";

    public string AiExplanation { get; set; } = string.Empty;

    public decimal AiConfidence { get; set; }

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;
}
