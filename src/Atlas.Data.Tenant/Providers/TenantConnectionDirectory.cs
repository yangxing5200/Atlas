using Atlas.Core.Entities.Global;
using Atlas.Data.Global;
using Atlas.Infrastructure.Caching.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Atlas.Data.Tenant.Providers;

/// <summary>
/// EF-backed tenant connection directory with cache support.
/// </summary>
public sealed class TenantConnectionDirectory : ITenantConnectionDirectory
{
    private readonly IConfiguration _configuration;
    private readonly AtlasGlobalDbContext _globalDbContext;
    private readonly ICacheService _cacheService;

    public TenantConnectionDirectory(
        IConfiguration configuration,
        AtlasGlobalDbContext globalDbContext,
        ICacheService cacheService)
    {
        _configuration = configuration;
        _globalDbContext = globalDbContext;
        _cacheService = cacheService;
    }

    public async Task<TenantConnectionInfo> GetConnectionInfoAsync(
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        var result = await _cacheService.GetOrSetAsync(
            TenantCacheKeys.TenantDbConnection,
            async () => await LoadTenantConnectionInfoAsync(tenantId, cancellationToken),
            instanceValue: tenantId,
            cancellationToken: cancellationToken);

        if (result.Value == null)
        {
            throw new InvalidOperationException($"未找到租户 {tenantId} 的数据库配置");
        }

        return result.Value;
    }

    private async Task<TenantConnectionInfo> LoadTenantConnectionInfoAsync(
        long tenantId,
        CancellationToken cancellationToken = default)
    {
        var tenant = await _globalDbContext.Tenants
            .AsNoTracking()
            .Include(t => t.DatabaseInstance)
            .FirstOrDefaultAsync(t => t.Id == tenantId, cancellationToken);

        if (tenant == null)
        {
            throw new InvalidOperationException($"租户 {tenantId} 不存在");
        }

        if (tenant.DatabaseInstance == null)
        {
            throw new InvalidOperationException($"租户 {tenantId} 未配置数据库实例");
        }

        var dbInstance = tenant.DatabaseInstance;
        var networkEnvCode = _configuration["Database:NetworkEnv"] ?? NetworkEnvCodes.Default;

        var connInfo = new TenantConnectionInfo
        {
            TenantId = tenant.Id,
            TenantName = tenant.Name,
            DatabaseInstanceId = dbInstance.Id,
            DbType = dbInstance.DbType,
            MasterServerCode = dbInstance.MasterServerCode,
            DbName = dbInstance.DbName
        };

        if (!string.IsNullOrEmpty(dbInstance.ConnectionString))
        {
            connInfo.MasterConnectionString = dbInstance.ConnectionString;
        }
        else
        {
            connInfo.MasterConnectionString = await GetServerConnectionStringAsync(
                dbInstance.MasterServerCode,
                networkEnvCode,
                dbInstance.DbType,
                cancellationToken);
        }

        if (!connInfo.MasterConnectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) &&
            !connInfo.MasterConnectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
        {
            connInfo.MasterConnectionString = AppendDatabase(connInfo.MasterConnectionString, dbInstance.DbName);
        }

        var readonlyServers = await _globalDbContext.Set<DatabaseReadonlyServer>()
            .AsNoTracking()
            .Where(s => s.MasterServerCode == dbInstance.MasterServerCode)
            .ToListAsync(cancellationToken);

        foreach (var readonlyServer in readonlyServers)
        {
            var connString = await GetServerConnectionStringAsync(
                readonlyServer.Code,
                networkEnvCode,
                dbInstance.DbType,
                cancellationToken);

            connString = AppendDatabase(connString, dbInstance.DbName);

            var serverInfo = new ReadonlyServerInfo
            {
                ServerCode = readonlyServer.Code,
                ConnectionString = connString,
                IsReport = readonlyServer.IsReport,
                IsPublic = readonlyServer.IsPublic
            };

            if (readonlyServer.IsReport)
            {
                connInfo.ReportServers.Add(serverInfo);
            }
            else
            {
                connInfo.ReadonlyServers.Add(serverInfo);
            }
        }

        return connInfo;
    }

    private async Task<string> GetServerConnectionStringAsync(
        string serverCode,
        string networkEnvCode,
        string dbType,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"{serverCode}:{networkEnvCode}";

        var result = await _cacheService.GetOrSetAsync(
            TenantCacheKeys.DatabaseServerConfig,
            async () => await LoadServerConnectionStringAsync(serverCode, networkEnvCode, dbType, cancellationToken),
            instanceValue: cacheKey,
            cancellationToken: cancellationToken);

        if (string.IsNullOrEmpty(result.Value))
        {
            throw new InvalidOperationException(
                $"未找到服务器 {serverCode} 在网络环境 {networkEnvCode} 下的配置");
        }

        return result.Value;
    }

    private async Task<string> LoadServerConnectionStringAsync(
        string serverCode,
        string networkEnvCode,
        string dbType,
        CancellationToken cancellationToken = default)
    {
        var config = await _globalDbContext.Set<DatabaseServerConfig>()
            .AsNoTracking()
            .FirstOrDefaultAsync(c =>
                c.ServerCode == serverCode &&
                c.NetworkEnvCode == networkEnvCode &&
                c.DbType == dbType,
                cancellationToken);

        if (config == null)
        {
            config = await _globalDbContext.Set<DatabaseServerConfig>()
                .AsNoTracking()
                .FirstOrDefaultAsync(c =>
                    c.ServerCode == serverCode &&
                    c.NetworkEnvCode == NetworkEnvCodes.Default &&
                    c.DbType == dbType,
                    cancellationToken);
        }

        return config?.ConnString ?? string.Empty;
    }

    private static string AppendDatabase(string connectionString, string databaseName)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return connectionString;

        var trimmed = connectionString.Trim().TrimEnd(';');
        return $"{trimmed};Database={databaseName};";
    }
}
