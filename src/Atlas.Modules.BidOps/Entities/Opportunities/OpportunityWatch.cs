using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Opportunities;

public sealed class OpportunityWatch : BidOpsTenantEntity
{
    public long OpportunityId { get; set; }

    public long UserId { get; set; }

    public string Remark { get; set; } = string.Empty;

    public bool Enabled { get; set; } = true;
}
