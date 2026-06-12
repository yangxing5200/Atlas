using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Matching;

public sealed class GoNoGoDecision : BidOpsTenantEntity
{
    public long PackageId { get; set; }

    public long? OpportunityId { get; set; }

    public long? MatchRunId { get; set; }

    public long? SupplierMatchResultId { get; set; }

    public long? SupplierId { get; set; }

    public string Decision { get; set; } = BidOpsGoNoGoDecisions.Hold;

    public string Reason { get; set; } = string.Empty;

    public string RiskSummary { get; set; } = string.Empty;

    public long DecidedByUserId { get; set; }

    public string DecidedByUserName { get; set; } = string.Empty;

    public DateTime DecidedAtUtc { get; set; }
}

public static class BidOpsGoNoGoDecisions
{
    public const string Go = "Go";
    public const string NoGo = "NoGo";
    public const string Hold = "Hold";
}
