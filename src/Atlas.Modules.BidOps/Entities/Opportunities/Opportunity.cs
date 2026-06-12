using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Opportunities;

public sealed class Opportunity : BidOpsTenantEntity
{
    public long NoticeId { get; set; }

    public long PackageId { get; set; }

    public string OpportunityNo { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    public string Stage { get; set; } = BidOpsOpportunityStages.New;

    public string Status { get; set; } = BidOpsOpportunityStatuses.Active;

    public string? ActiveMarker { get; set; } = BidOpsOpportunityActiveMarkers.Active;

    public int Priority { get; set; } = 3;

    public decimal? EstimatedAmount { get; set; }

    public decimal? ValueScore { get; set; }

    public string ValueLevel { get; set; } = BidOpsOpportunityValueLevels.Unknown;

    public string Decision { get; set; } = BidOpsOpportunityDecisions.Undecided;

    public long? OwnerUserId { get; set; }

    public DateTime? NextActionAtUtc { get; set; }

    public DateTime LastStageChangedAtUtc { get; set; }

    public string AssessmentSummary { get; set; } = string.Empty;

    public string Remark { get; set; } = string.Empty;
}

public static class BidOpsOpportunityStatuses
{
    public const string Active = "Active";
    public const string Closed = "Closed";
    public const string Archived = "Archived";
}

public static class BidOpsOpportunityStages
{
    public const string New = "New";
    public const string Watching = "Watching";
    public const string Assessing = "Assessing";
    public const string Decided = "Decided";
    public const string PursuitReady = "PursuitReady";
    public const string Closed = "Closed";
}

public static class BidOpsOpportunityDecisions
{
    public const string Undecided = "Undecided";
    public const string Go = "Go";
    public const string NoGo = "NoGo";
    public const string Hold = "Hold";
}

public static class BidOpsOpportunityValueLevels
{
    public const string Unknown = "Unknown";
    public const string Low = "Low";
    public const string Medium = "Medium";
    public const string High = "High";
}

public static class BidOpsOpportunityActiveMarkers
{
    public const string Active = "active";
}
