using Atlas.Core.Services;
using Atlas.Data.Common.Interceptors;
using Microsoft.EntityFrameworkCore;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// 租户数据库上下文工厂
    /// 负责创建主库、只读库、报表库的DbContext实例
    /// 使用请求级缓存避免重复获取连接串
    /// </summary>
    public class TenantDbContextFactory : ITenantDbContextFactory
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
        public async Task<AtlasTenantDbContext> CreateDbContextAsync(CancellationToken cancellationToken = default)
        {
            _cachedMasterConnString ??= await _connProvider.GetConnStringAsync(cancellationToken);
            return CreateContext(_cachedMasterConnString, isReadonly: false);
        }

        /// <summary>
        /// 创建只读库上下文
        /// 如果在事务中则使用主库，否则使用只读库连接串
        /// 禁用变更跟踪以优化性能
        /// </summary>
        public async Task<AtlasTenantDbContext> CreateReadonlyDbContextAsync(CancellationToken cancellationToken = default)
        {
            if (IsInTransaction())
            {
                return await CreateDbContextAsync(cancellationToken);
            }

            _cachedReadonlyConnString ??= await _connProvider.GetReadonlyConnStringAsync(cancellationToken);
            return CreateContext(_cachedReadonlyConnString, isReadonly: true);
        }

        /// <summary>
        /// 创建报表库上下文
        /// 如果在事务中则使用主库，否则使用报表库连接串
        /// 禁用变更跟踪以优化性能
        /// </summary>
        public async Task<AtlasTenantDbContext> CreateReportDbContextAsync(CancellationToken cancellationToken = default)
        {
            if (IsInTransaction())
            {
                return await CreateDbContextAsync(cancellationToken);
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
        public AtlasTenantDbContext CreateReadonlyDbContextSync()
        {
            if (IsInTransaction())
            {
                if (_cachedMasterConnString == null)
                {
                    throw new InvalidOperationException(
                        "在事务中首次创建DbContext必须使用异步方法 CreateDbContextAsync");
                }
                return CreateContext(_cachedMasterConnString, isReadonly: false);
            }

            if (_cachedReadonlyConnString == null)
            {
                throw new InvalidOperationException(
                    "首次创建ReadonlyDbContext必须使用异步方法 CreateReadonlyDbContextAsync");
            }

            return CreateContext(_cachedReadonlyConnString, isReadonly: true);
        }

        /// <summary>
        /// 创建DbContext实例
        /// </summary>
        /// <param name="connectionString">数据库连接串</param>
        /// <param name="isReadonly">是否为只读上下文</param>
        private AtlasTenantDbContext CreateContext(string connectionString, bool isReadonly)
        {
            var optionsBuilder = new DbContextOptionsBuilder<AtlasTenantDbContext>();

            // 配置 MySQL 连接
            optionsBuilder.UseMySql(
                connectionString,
                ServerVersion.AutoDetect(connectionString),  // 自动检测 MySQL 版本
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

        /// <summary>
        /// 检查是否在分布式事务中
        /// 在事务中必须使用主库以保证数据一致性
        /// </summary>
        private bool IsInTransaction()
        {
            return System.Transactions.Transaction.Current != null;
        }
    }
}