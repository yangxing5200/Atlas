using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Pursuits;

public sealed class Pursuit : BidOpsTenantEntity
{
    public long NoticeId { get; set; }

    public long PackageId { get; set; }

    public long? OpportunityId { get; set; }

    public long? GoNoGoDecisionId { get; set; }

    public long? SupplierId { get; set; }

    public string SupplierNameSnapshot { get; set; } = string.Empty;

    public string PursuitNo { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Stage { get; set; } = BidOpsPursuitStages.New;

    public string Status { get; set; } = BidOpsPursuitStatuses.Active;

    public string? ActiveMarker { get; set; } = BidOpsPursuitActiveMarkers.Active;

    public int Priority { get; set; } = 3;

    public decimal? EstimatedAmount { get; set; }

    public DateTime? BidDeadlineAtUtc { get; set; }

    public long? OwnerUserId { get; set; }

    public int ProgressPercent { get; set; }

    public string RiskLevel { get; set; } = BidOpsPursuitRiskLevels.None;

    public DateTime LastStageChangedAtUtc { get; set; }

    public string Remark { get; set; } = string.Empty;
}

public static class BidOpsPursuitStages
{
    public const string New = "New";
    public const string Preparing = "Preparing";
    public const string Review = "Review";
    public const string Submitted = "Submitted";
    public const string Awarded = "Awarded";
    public const string Closed = "Closed";
}

public static class BidOpsPursuitStatuses
{
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string Archived = "Archived";
}

public static class BidOpsPursuitRiskLevels
{
    public const string None = "None";
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
}

public static class BidOpsPursuitActiveMarkers
{
    public const string Active = "active";
}
