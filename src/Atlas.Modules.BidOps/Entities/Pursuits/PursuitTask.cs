using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Pursuits;

public sealed class PursuitTask : BidOpsTenantEntity
{
    public long PursuitId { get; set; }

    public string Title { get; set; } = string.Empty;

    public string TaskType { get; set; } = BidOpsPursuitTaskTypes.Other;

    public string Status { get; set; } = BidOpsPursuitTaskStatuses.Todo;

    public int Priority { get; set; } = 3;

    public long? OwnerUserId { get; set; }

    public DateTime? DueAtUtc { get; set; }

    public DateTime? CompletedAtUtc { get; set; }

    public string Description { get; set; } = string.Empty;

    public string ResultNote { get; set; } = string.Empty;
}

public static class BidOpsPursuitTaskTypes
{
    public const string Qualification = "Qualification";
    public const string Technical = "Technical";
    public const string Commercial = "Commercial";
    public const string Pricing = "Pricing";
    public const string Review = "Review";
    public const string Submission = "Submission";
    public const string Other = "Other";
}

public static class BidOpsPursuitTaskStatuses
{
    public const string Todo = "Todo";
    public const string InProgress = "InProgress";
    public const string Done = "Done";
    public const string Blocked = "Blocked";
    public const string Canceled = "Canceled";
    public const string Overdue = "Overdue";
}
