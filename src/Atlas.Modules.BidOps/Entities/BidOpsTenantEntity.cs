using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;

namespace Atlas.Modules.BidOps.Entities;

public abstract class BidOpsTenantEntity : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }
}
