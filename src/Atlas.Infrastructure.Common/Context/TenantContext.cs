namespace Atlas.Infrastructure.Common.Context;

using Atlas.Core.Context;

public class TenantContext : ITenantContext
{
    private long? _tenantId;
    private string? _connectionString;

    public long? TenantId => _tenantId;
    public string? TenantConnectionString => _connectionString;

    public void SetTenant(long tenantId, string connectionString)
    {
        _tenantId = tenantId;
        _connectionString = connectionString;
    }

    public void Clear()
    {
        _tenantId = null;
        _connectionString = null;
    }
}