namespace Atlas.ModuleTemplate.Models;

public sealed class TenantRecordDto
{
    public long Id { get; init; }

    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; }
}
