namespace Atlas.Core.Context;

public interface ITenantContext
{
    long? TenantId { get; }
    string? TenantConnectionString { get; }
    void SetTenant(long tenantId, string connectionString);
    void Clear();
}