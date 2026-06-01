namespace Atlas.ModuleTemplate.Models;

public sealed class TenantRecordSearchQuery
{
    public string? Keyword { get; init; }

    public bool? IsActive { get; init; }

    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}
