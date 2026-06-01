using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;

namespace Atlas.ModuleTemplate.Entities;

public sealed class SharedRecord : SharedEntity, ISnowflakeId
{
    public string Name { get; set; } = string.Empty;
}
