using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Matching;

public sealed class MissingEvidenceCheck : BidOpsTenantEntity
{
    public long RunId { get; set; }

    public long ResultId { get; set; }

    public long PackageId { get; set; }

    public long SupplierId { get; set; }

    public long? RequirementId { get; set; }

    public long? MatchedEvidenceDocumentId { get; set; }

    public string RequiredEvidenceType { get; set; } = string.Empty;

    public string RequirementText { get; set; } = string.Empty;

    public string Status { get; set; } = BidOpsMissingEvidenceStatuses.Missing;

    public string Explanation { get; set; } = string.Empty;
}

public static class BidOpsMissingEvidenceStatuses
{
    public const string Missing = "Missing";
    public const string Expired = "Expired";
    public const string ExpiringSoon = "ExpiringSoon";
}
