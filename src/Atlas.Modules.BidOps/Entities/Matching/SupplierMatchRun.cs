using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Matching;

public sealed class SupplierMatchRun : BidOpsTenantEntity
{
    public long PackageId { get; set; }

    public long? BackgroundJobId { get; set; }

    public string RunNo { get; set; } = string.Empty;

    public string Status { get; set; } = BidOpsSupplierMatchRunStatuses.Queued;

    public long RequestedByUserId { get; set; }

    public string RequestedByUserName { get; set; } = string.Empty;

    public string CriteriaSummary { get; set; } = string.Empty;

    public int MaxSuppliers { get; set; } = 100;

    public int SupplierCount { get; set; }

    public int MatchedCount { get; set; }

    public int MissingEvidenceCount { get; set; }

    public DateTime? StartedAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string ErrorMessage { get; set; } = string.Empty;
}

public static class BidOpsSupplierMatchRunStatuses
{
    public const string Queued = "Queued";
    public const string Running = "Running";
    public const string Succeeded = "Succeeded";
    public const string Failed = "Failed";
}
