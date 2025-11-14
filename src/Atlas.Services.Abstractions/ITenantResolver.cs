namespace Atlas.Services.Abstractions;

public interface ITenantResolver
{
    Task<TenantInfo?> ResolveTenantAsync(long tenantId);
}

public record TenantInfo
{
    public long Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string ConnectionString { get; init; } = string.Empty;
    public bool IsActive { get; init; }
}