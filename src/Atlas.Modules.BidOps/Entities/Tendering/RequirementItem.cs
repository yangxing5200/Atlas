using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Tendering;

public sealed class RequirementItem : BidOpsTenantEntity
{
    public long PackageId { get; set; }

    public long? RequirementStagingId { get; set; }

    public string RequirementType { get; set; } = string.Empty;

    public string OriginalText { get; set; } = string.Empty;

    public long? SourceFileId { get; set; }

    public int? SourcePage { get; set; }

    public bool IsMandatory { get; set; }

    public bool IsRejectRisk { get; set; }

    public string RequiredEvidenceType { get; set; } = string.Empty;

    public string RiskLevel { get; set; } = "Medium";

    public string AiExplanation { get; set; } = string.Empty;

    public string ManualRemark { get; set; } = string.Empty;
}
