namespace Atlas.ModuleTemplate.Models;

public sealed class UpdateTenantRecordRequest
{
    public string Name { get; init; } = string.Empty;

    public bool IsActive { get; init; } = true;
}
