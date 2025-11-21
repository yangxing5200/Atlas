using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Services;
using Atlas.Data.Abstractions.Repositories;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Identity;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Providers
{
    public class TenantDbContextProvider : ITenantDbContextProvider
    {
        private readonly TenantDbContextAccessor _accessor;
        private readonly AuditInterceptor _auditInterceptor;
        private readonly ITenantDbConnProvider _tenantDbConnProvider;
        private readonly ICurrentIdentity _identity;

        // 使用线程安全的字典缓存 ServerVersion，避免重复检测
        private static readonly ConcurrentDictionary<string, ServerVersion> _serverVersionCache = new();

        public TenantDbContextProvider(
            TenantDbContextAccessor accessor,
            AuditInterceptor auditInterceptor,
            ITenantDbConnProvider tenantDbConnProvider,
            ITenantRepository tenantRepo,
            ICurrentIdentity identity)
        {
            _accessor = accessor ?? throw new ArgumentNullException(nameof(accessor));
            _auditInterceptor = auditInterceptor ?? throw new ArgumentNullException(nameof(auditInterceptor));
            _tenantDbConnProvider = tenantDbConnProvider ?? throw new ArgumentNullException(nameof(tenantDbConnProvider));
            _identity = identity ?? throw new ArgumentNullException(nameof(identity));
        }

        public async Task<AtlasTenantDbContext> GetWriteDbContext()
        {
            // 检查缓存
            if (_accessor.WriteDbContext != null)
                return _accessor.WriteDbContext;

            var connectionString = await _tenantDbConnProvider.GetConnStringAsync();

            // 验证连接字符串
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("数据库连接字符串不能为空");

            var optionsBuilder = new DbContextOptionsBuilder<AtlasTenantDbContext>();

            // 使用缓存的 ServerVersion
            var serverVersion = GetOrCacheServerVersion(connectionString);

            // 配置 MySQL 连接
            optionsBuilder.UseMySql(
                connectionString,
                serverVersion,
                mySqlOptions =>
                {
                    // 启用连接重试机制
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);

                    // 命令超时时间（秒）
                    mySqlOptions.CommandTimeout(30);

                    // 启用字符串比较转换（性能优化）
                    mySqlOptions.EnableStringComparisonTranslations();
                })
                .AddInterceptors(_auditInterceptor); // 写操作需要审计拦截器

            var context = new AtlasTenantDbContext(optionsBuilder.Options, _identity);

            // 缓存并返回
            return _accessor.WriteDbContext = context;
        }

        public async Task<AtlasTenantDbContext> GetReadDbContext()
        {
            // 检查缓存
            if (_accessor.ReadDbContext != null)
                return _accessor.ReadDbContext;

            var connectionString = await _tenantDbConnProvider.GetReadonlyConnStringAsync();

            // 验证连接字符串
            if (string.IsNullOrWhiteSpace(connectionString))
                throw new InvalidOperationException("只读数据库连接字符串不能为空");

            var optionsBuilder = new DbContextOptionsBuilder<AtlasTenantDbContext>();

            // 使用缓存的 ServerVersion
            var serverVersion = GetOrCacheServerVersion(connectionString);

            // 配置 MySQL 连接
            optionsBuilder.UseMySql(
                connectionString,
                serverVersion,
                mySqlOptions =>
                {
                    // 启用连接重试机制
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);

                    // 命令超时时间（秒）
                    mySqlOptions.CommandTimeout(30);

                    // 启用字符串比较转换（性能优化）
                    mySqlOptions.EnableStringComparisonTranslations();
                });
            // 注意：读上下文不添加 AuditInterceptor

            var context = new AtlasTenantDbContext(optionsBuilder.Options, _identity);

            // 配置只读上下文行为
            context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
            context.ChangeTracker.AutoDetectChangesEnabled = false;

            // 缓存并返回
            return _accessor.ReadDbContext = context;
        }

        /// <summary>
        /// 获取或缓存 MySQL ServerVersion，避免每次都执行数据库查询
        /// </summary>
        /// <param name="connectionString">数据库连接字符串</param>
        /// <returns>MySQL ServerVersion</returns>
        private ServerVersion GetOrCacheServerVersion(string connectionString)
        {
            // 使用连接字符串的哈希作为缓存键（避免泄露敏感信息）
            var cacheKey = GetConnectionStringHash(connectionString);

            return _serverVersionCache.GetOrAdd(cacheKey, _ =>
            {
                try
                {
                    return ServerVersion.AutoDetect(connectionString);
                }
                catch (Exception ex)
                {
                    throw new InvalidOperationException(
                        "无法自动检测 MySQL 服务器版本，请检查数据库连接", ex);
                }
            });
        }

        /// <summary>
        /// 获取连接字符串的哈希值作为缓存键
        /// </summary>
        private string GetConnectionStringHash(string connectionString)
        {
            // 提取关键部分作为缓存键（Server + Database）
            // 这样即使密码变化，只要服务器和数据库相同，就能复用缓存
            var builder = new System.Data.Common.DbConnectionStringBuilder
            {
                ConnectionString = connectionString
            };

            var server = builder.TryGetValue("Server", out var s) ? s?.ToString() : "";
            var database = builder.TryGetValue("Database", out var d) ? d?.ToString() : "";
            var port = builder.TryGetValue("Port", out var p) ? p?.ToString() : "3306";

            return $"{server}:{port}:{database}";
        }
    }
}