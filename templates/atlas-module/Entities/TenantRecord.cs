using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;

namespace Atlas.ModuleTemplate.Entities;

public sealed class TenantRecord : BaseEntity, ITenantEntity, ISnowflakeId
{
    public long TenantId { get; set; }

    public string Name { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
