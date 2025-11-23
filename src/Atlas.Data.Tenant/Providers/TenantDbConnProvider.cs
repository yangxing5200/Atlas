using Atlas.Core.Entities.Global;
using Atlas.Core.Services;
using Atlas.Data.Global;
using Atlas.Infrastructure.Caching.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

namespace Atlas.Data.Tenant.Providers
{
    public class TenantDbConnProvider : ITenantDbConnProvider
    {
        private readonly ICurrentIdentity _currentIdentity;
        private readonly IConfiguration _configuration;
        private readonly AtlasGlobalDbContext _globalDbContext;
        private readonly ICacheService _cacheService;

        public TenantDbConnProvider(
            ICurrentIdentity currentIdentity,
            IConfiguration configuration,
            AtlasGlobalDbContext globalDbContext,
            ICacheService cacheService)
        {
            _currentIdentity = currentIdentity;
            _configuration = configuration;
            _globalDbContext = globalDbContext;
            _cacheService = cacheService;
        }

        public long? TenantId => _currentIdentity.TenantId;

        public async Task<string> GetConnStringAsync(CancellationToken cancellationToken = default)
        {
            if (!TenantId.HasValue)
            {
                throw new InvalidOperationException("当前上下文中没有租户信息");
            }

            var connInfo = await GetTenantConnectionInfoAsync(TenantId.Value, cancellationToken);
            return connInfo.MasterConnectionString;
        }

        public async Task<string> GetReadonlyConnStringAsync(CancellationToken cancellationToken = default)
        {
            if (!TenantId.HasValue)
            {
                throw new InvalidOperationException("当前上下文中没有租户信息");
            }

            var connInfo = await GetTenantConnectionInfoAsync(TenantId.Value, cancellationToken);

            // 如果有配置只读库,随机选择一个(简单负载均衡)
            var readonlyServers = connInfo.ReadonlyServers
                .Where(s => !s.IsReport)
                .ToList();

            if (readonlyServers.Any())
            {
                var selectedServer = readonlyServers[Random.Shared.Next(readonlyServers.Count)];
                return selectedServer.ConnectionString;
            }

            // 如果没有只读库,fallback到主库
            return connInfo.MasterConnectionString;
        }

        public async Task<string> GetReportConnStringAsync(CancellationToken cancellationToken = default)
        {
            if (!TenantId.HasValue)
            {
                throw new InvalidOperationException("当前上下文中没有租户信息");
            }

            var connInfo = await GetTenantConnectionInfoAsync(TenantId.Value, cancellationToken);

            // 优先使用报表库
            var reportServers = connInfo.ReportServers;
            if (reportServers.Any())
            {
                var selectedServer = reportServers[Random.Shared.Next(reportServers.Count)];
                return selectedServer.ConnectionString;
            }

            // 如果没有报表库,fallback到只读库
            return await GetReadonlyConnStringAsync(cancellationToken);
        }

        /// <summary>
        /// 获取租户连接信息(带缓存)
        /// </summary>
        private async Task<TenantConnectionInfo> GetTenantConnectionInfoAsync(
            long tenantId,
            CancellationToken cancellationToken = default)
        {
            var result = await _cacheService.GetOrSetAsync(
                TenantCacheKeys.TenantDbConnection,
                async () => await LoadTenantConnectionInfoAsync(tenantId, cancellationToken),
                instanceValue: tenantId,
                cancellationToken: cancellationToken
            );

            if (result.Value == null)
            {
                throw new InvalidOperationException($"未找到租户 {tenantId} 的数据库配置");
            }

            return result.Value;
        }

        /// <summary>
        /// 从数据库加载租户连接信息
        /// </summary>
        private async Task<TenantConnectionInfo> LoadTenantConnectionInfoAsync(
            long tenantId,
            CancellationToken cancellationToken = default)
        {
            // 查询租户及其关联的数据库实例
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

            // 获取网络环境编码(从配置读取,默认为default)
            var networkEnvCode = _configuration["Database:NetworkEnv"] ?? NetworkEnvCodes.Default;

            // 构建连接信息
            var connInfo = new TenantConnectionInfo
            {
                TenantId = tenant.Id,
                TenantName = tenant.Name,
                DatabaseInstanceId = dbInstance.Id,
                DbType = dbInstance.DbType,
                MasterServerCode = dbInstance.MasterServerCode,
                DbName = dbInstance.DbName
            };

            // 获取主库连接串
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

            // 如果MasterConnectionString中没有Database,则追加DbName
            if (!connInfo.MasterConnectionString.Contains("Database=", StringComparison.OrdinalIgnoreCase) &&
                !connInfo.MasterConnectionString.Contains("Initial Catalog=", StringComparison.OrdinalIgnoreCase))
            {
                connInfo.MasterConnectionString = AppendDatabase(connInfo.MasterConnectionString, dbInstance.DbName);
            }

            // 查询只读库配置
            var readonlyServers = await _globalDbContext.Set<DatabaseReadonlyServer>()
                .AsNoTracking()
                .Where(s => s.MasterServerCode == dbInstance.MasterServerCode)
                .ToListAsync(cancellationToken);

            // 加载只读库连接串
            foreach (var readonlyServer in readonlyServers)
            {
                var connString = await GetServerConnectionStringAsync(
                    readonlyServer.Code,
                    networkEnvCode,
                    dbInstance.DbType,
                    cancellationToken);

                // 追加数据库名
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

        /// <summary>
        /// 获取服务器连接串(带缓存)
        /// </summary>
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
                cancellationToken: cancellationToken
            );

            if (string.IsNullOrEmpty(result.Value))
            {
                throw new InvalidOperationException(
                    $"未找到服务器 {serverCode} 在网络环境 {networkEnvCode} 下的配置");
            }

            return result.Value;
        }

        /// <summary>
        /// 从数据库加载服务器连接串
        /// </summary>
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
                // 尝试使用默认网络环境
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

        /// <summary>
        /// 在连接串中追加数据库名
        /// </summary>
        private string AppendDatabase(string connectionString, string databaseName)
        {
            if (string.IsNullOrEmpty(databaseName))
            {
                return connectionString;
            }

            var separator = connectionString.EndsWith(";") ? "" : ";";
            return $"{connectionString}{separator}Database={databaseName}";
        }
    }
}