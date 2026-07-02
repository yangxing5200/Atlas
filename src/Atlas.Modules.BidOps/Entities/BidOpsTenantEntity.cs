using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;

namespace Atlas.Modules.BidOps.Entities;

/// <summary>
/// BidOps 租户业务实体基类。
/// </summary>
public abstract class BidOpsTenantEntity : BaseEntity, ITenantEntity, ISnowflakeId
{
    /// <summary>
    /// 租户主键，用于 Atlas 租户数据隔离。
    /// </summary>
    public long TenantId { get; set; }
}
