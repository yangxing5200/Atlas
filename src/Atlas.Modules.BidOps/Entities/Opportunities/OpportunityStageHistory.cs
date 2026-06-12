using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Opportunities;

public sealed class OpportunityStageHistory : BidOpsTenantEntity
{
    public long OpportunityId { get; set; }

    public string FromStage { get; set; } = string.Empty;

    public string ToStage { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public long? OperatorUserId { get; set; }

    public DateTime OccurredAtUtc { get; set; }
}
