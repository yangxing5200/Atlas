using Atlas.Core.Services;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Tenant.Providers;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Context
{
    /// <summary>
    /// 租户数据库上下文工厂
    /// 负责创建主库、只读库、报表库的DbContext实例
    /// 使用请求级缓存避免重复获取连接串
    /// </summary>
    public class TenantDbContextFactory : ITenantDbContextFactory, IDisposable
    {
        private readonly ICurrentIdentity _currentIdentity;
        private readonly ITenantDbConnProvider _connProvider;
        private readonly AuditInterceptor _auditInterceptor;
        /// <summary>
        /// 请求级缓存：主库连接串
        /// </summary>
        private string? _cachedMasterConnString;

        /// <summary>
        /// 请求级缓存：只读库连接串
        /// </summary>
        private string? _cachedReadonlyConnString;

        /// <summary>
        /// 请求级缓存：报表库连接串
        /// </summary>
        private string? _cachedReportConnString;

        private AtlasTenantDbContext? _cachedWriteContext;
        private AtlasTenantDbContext? _cachedReadonlyContext;
        private bool _disposed;
        private static readonly ConcurrentDictionary<string, ServerVersion> _serverVersionCache = new();
        private readonly SemaphoreSlim _masterLock = new(1, 1);
        public TenantDbContextFactory(
            ICurrentIdentity currentIdentity,
            ITenantDbConnProvider connProvider,
            AuditInterceptor auditInterceptor
            )
        {
            _currentIdentity = currentIdentity;
            _connProvider = connProvider;
            _auditInterceptor = auditInterceptor;
        }

        /// <summary>
        /// 创建主库上下文（读写）
        /// 使用主库连接串，启用变更跟踪
        /// </summary>
        public async Task<AtlasTenantDbContext> GetDbContextAsync(CancellationToken cancellationToken)
        {
            if (_cachedWriteContext != null) return _cachedWriteContext;

            await _masterLock.WaitAsync(cancellationToken);
            try
            {
                if (_cachedMasterConnString == null)
                    _cachedMasterConnString = await _connProvider.GetConnStringAsync(cancellationToken);

                if (_cachedWriteContext == null)
                    _cachedWriteContext = CreateContext(_cachedMasterConnString, false);

                return _cachedWriteContext;
            }
            finally
            {
                _masterLock.Release();
            }
        }

        /// <summary>
        /// 创建只读库上下文
        /// 如果在事务中则使用主库，否则使用只读库连接串
        /// 禁用变更跟踪以优化性能
        /// </summary>
        public async Task<AtlasTenantDbContext> GetReadonlyDbContextAsync(CancellationToken cancellationToken = default)
        {
            if (IsInTransaction())
            {
                return await GetDbContextAsync(cancellationToken);
            }

            if (_cachedReadonlyContext != null)
                return _cachedReadonlyContext;

            _cachedReadonlyConnString ??= await _connProvider.GetReadonlyConnStringAsync(cancellationToken);
            _cachedReadonlyContext = CreateContext(_cachedReadonlyConnString, isReadonly: true);
            return _cachedReadonlyContext;
        }

        /// <summary>
        /// 创建报表库上下文
        /// 如果在事务中则使用主库，否则使用报表库连接串
        /// 禁用变更跟踪以优化性能
        /// </summary>
        public async Task<AtlasTenantDbContext> GetReportDbContextAsync(CancellationToken cancellationToken = default)
        {
            if (IsInTransaction())
            {
                return await GetDbContextAsync(cancellationToken);
            }

            _cachedReportConnString ??= await _connProvider.GetReportConnStringAsync(cancellationToken);
            return CreateContext(_cachedReportConnString, isReadonly: true);
        }

        /// <summary>
        /// 同步创建只读库上下文
        /// 仅在连接串已缓存时使用，避免阻塞
        /// 主要用于Repository延迟初始化场景
        /// </summary>
        /// <exception cref="InvalidOperationException">连接串未缓存时抛出异常</exception>
        public AtlasTenantDbContext GetReadonlyDbContext()
        {
            if (_cachedReadonlyContext != null)
                return _cachedReadonlyContext;

            if (_cachedReadonlyConnString == null)
            {
                throw new InvalidOperationException(
                    "首次创建ReadonlyDbContext必须使用异步方法");
            }

            _cachedReadonlyContext = CreateContext(_cachedReadonlyConnString, isReadonly: true);
            return _cachedReadonlyContext;
        }

        /// <summary>
        /// 同步创建只读库上下文
        /// 仅在连接串已缓存时使用，避免阻塞
        /// 主要用于Repository延迟初始化场景
        /// </summary>
        /// <exception cref="InvalidOperationException">连接串未缓存时抛出异常</exception>
        public AtlasTenantDbContext GetDbContext()
        {
            if (_cachedWriteContext != null)
                return _cachedWriteContext;

            if (_cachedMasterConnString == null)
            {
                throw new InvalidOperationException(
                    "首次创建DbContext必须使用异步方法");
            }

            _cachedWriteContext = CreateContext(_cachedMasterConnString, isReadonly: false);
            return _cachedWriteContext;
        }

        /// <summary>
        /// 创建DbContext实例
        /// </summary>
        /// <param name="connectionString">数据库连接串</param>
        /// <param name="isReadonly">是否为只读上下文</param>
        private AtlasTenantDbContext CreateContext(string connectionString, bool isReadonly)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AtlasTenantDbContext>();
            var serverVersion = _serverVersionCache.GetOrAdd(GetServerKey(connectionString),
                _ => ServerVersion.AutoDetect(connectionString));
            // 配置 MySQL 连接
            optionsBuilder.UseMySql(connectionString, serverVersion,
                mySqlOptions =>
                {
                    // 启用连接重试机制
                    mySqlOptions.EnableRetryOnFailure(
                        maxRetryCount: 3,
                        maxRetryDelay: TimeSpan.FromSeconds(5),
                        errorNumbersToAdd: null);

                    // 命令超时时间（秒）
                    mySqlOptions.CommandTimeout(30);

                    // 启用详细错误信息（仅开发环境）
                    // mySqlOptions.EnableDetailedErrors();

                    // 启用字符串比较转换（性能优化）
                    mySqlOptions.EnableStringComparisonTranslations();
                }).AddInterceptors(_auditInterceptor);

            // 只读上下文禁用变更跟踪
            if (isReadonly)
            {
                optionsBuilder.UseQueryTrackingBehavior(QueryTrackingBehavior.NoTracking);
            }

            var context = new AtlasTenantDbContext(optionsBuilder.Options, _currentIdentity);

            // 确保只读上下文的 ChangeTracker 配置
            if (isReadonly)
            {
                context.ChangeTracker.QueryTrackingBehavior = QueryTrackingBehavior.NoTracking;
                context.ChangeTracker.AutoDetectChangesEnabled = false;
            }

            return context;
        }
        private string GetServerKey(string connectionString)
        {
            var builder = new MySqlConnectionStringBuilder(connectionString);
            return $"{builder.Server}:{builder.Port}";
        }
        /// <summary>
        /// 检查是否在分布式事务中
        /// 在事务中必须使用主库以保证数据一致性
        /// </summary>
        private bool IsInTransaction()
        {
            if (System.Transactions.Transaction.Current != null)
                return true;

            // 检测 EF Core 事务
            if (_cachedWriteContext?.Database.CurrentTransaction != null)
                return true;

            return false;
        }

        public void Dispose()
        {
            if (_disposed) return;

            _cachedWriteContext?.Dispose();
            _cachedReadonlyContext?.Dispose();

            _disposed = true;
        }
    }
}