using Atlas.Core.Context;

namespace Atlas.Data.Tenant.Providers;

/// <summary>
/// Backward-compatible facade for tenant connection string resolution.
/// </summary>
public sealed class TenantDbConnProvider : ITenantDbConnProvider
{
    private readonly ITenantExecutionContext _executionContext;
    private readonly ITenantConnectionDirectory _connectionDirectory;

    public TenantDbConnProvider(
        ITenantExecutionContext executionContext,
        ITenantConnectionDirectory connectionDirectory)
    {
        _executionContext = executionContext;
        _connectionDirectory = connectionDirectory;
    }

    public long? TenantId => _executionContext.TenantId;

    public async Task<string> GetConnStringAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await GetConnStringAsync(tenantId, cancellationToken);
    }

    public async Task<string> GetReadonlyConnStringAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await GetReadonlyConnStringAsync(tenantId, cancellationToken);
    }

    public async Task<string> GetReportConnStringAsync(CancellationToken cancellationToken = default)
    {
        var tenantId = RequireTenantId();
        return await GetReportConnStringAsync(tenantId, cancellationToken);
    }

    public async Task<string> GetConnStringAsync(
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        var connInfo = await _connectionDirectory.GetConnectionInfoAsync(tenantId, cancellationToken);
        return connInfo.MasterConnectionString;
    }

    public async Task<string> GetReadonlyConnStringAsync(
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        var connInfo = await _connectionDirectory.GetConnectionInfoAsync(tenantId, cancellationToken);
        return SelectReadonlyConnectionString(connInfo);
    }

    public async Task<string> GetReportConnStringAsync(
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        var connInfo = await _connectionDirectory.GetConnectionInfoAsync(tenantId, cancellationToken);
        return SelectReportConnectionString(connInfo);
    }

    private long RequireTenantId()
    {
        if (!TenantId.HasValue)
        {
            throw new InvalidOperationException("当前上下文中没有租户信息");
        }

        return TenantId.Value;
    }

    private static string SelectReadonlyConnectionString(TenantConnectionInfo connInfo)
    {
        var readonlyServers = connInfo.ReadonlyServers
            .Where(s => !s.IsReport)
            .ToList();

        if (readonlyServers.Any())
        {
            var selectedServer = readonlyServers[Random.Shared.Next(readonlyServers.Count)];
            return selectedServer.ConnectionString;
        }

        return connInfo.MasterConnectionString;
    }

    private static string SelectReportConnectionString(TenantConnectionInfo connInfo)
    {
        if (connInfo.ReportServers.Any())
        {
            var selectedServer = connInfo.ReportServers[Random.Shared.Next(connInfo.ReportServers.Count)];
            return selectedServer.ConnectionString;
        }

        return SelectReadonlyConnectionString(connInfo);
    }
}
