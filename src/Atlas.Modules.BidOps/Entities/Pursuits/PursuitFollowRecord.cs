using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Pursuits;

public sealed class PursuitFollowRecord : BidOpsTenantEntity
{
    public long PursuitId { get; set; }

    public string FollowType { get; set; } = BidOpsPursuitFollowTypes.Note;

    public string Content { get; set; } = string.Empty;

    public DateTime? NextActionAtUtc { get; set; }

    public long? CreatedByUserId { get; set; }

    public string CreatedByUserName { get; set; } = string.Empty;
}

public static class BidOpsPursuitFollowTypes
{
    public const string Note = "Note";
    public const string Call = "Call";
    public const string Meeting = "Meeting";
    public const string StatusChange = "StatusChange";
    public const string Risk = "Risk";
    public const string Other = "Other";
}
