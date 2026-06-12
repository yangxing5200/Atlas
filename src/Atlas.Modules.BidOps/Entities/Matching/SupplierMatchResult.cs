using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Matching;

public sealed class SupplierMatchResult : BidOpsTenantEntity
{
    public long RunId { get; set; }

    public long PackageId { get; set; }

    public long SupplierId { get; set; }

    public string SupplierNameSnapshot { get; set; } = string.Empty;

    public int Rank { get; set; }

    public decimal Score { get; set; }

    public string MatchLevel { get; set; } = BidOpsSupplierMatchLevels.Low;

    public string Recommendation { get; set; } = BidOpsSupplierMatchRecommendations.NotRecommended;

    public bool CategoryMatched { get; set; }

    public bool RegionMatched { get; set; }

    public int EvidenceMatchedCount { get; set; }

    public int MissingEvidenceCount { get; set; }

    public string RiskFlags { get; set; } = string.Empty;

    public string Explanation { get; set; } = string.Empty;
}

public static class BidOpsSupplierMatchLevels
{
    public const string High = "High";
    public const string Medium = "Medium";
    public const string Low = "Low";
}

public static class BidOpsSupplierMatchRecommendations
{
    public const string Candidate = "Candidate";
    public const string Caution = "Caution";
    public const string NotRecommended = "NotRecommended";
}
