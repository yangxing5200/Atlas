using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Context
{
    /// <summary>
    /// 租户数据库上下文工厂接口
    /// </summary>
    public interface ITenantDbContextFactory
    {
        /// <summary>
        /// 异步创建主库上下文（读写）- 依赖 ICurrentIdentity
        /// </summary>
        Task<AtlasTenantDbContext> GetDbContextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步创建只读库上下文 - 依赖 ICurrentIdentity
        /// </summary>
        Task<AtlasTenantDbContext> GetReadonlyDbContextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步创建报表库上下文 - 依赖 ICurrentIdentity
        /// </summary>
        Task<AtlasTenantDbContext> GetReportDbContextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步创建主库上下文（读写）- 显式传入 tenantId，用于登录等场景
        /// </summary>
        Task<AtlasTenantDbContext> GetDbContextAsync(long tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步创建只读库上下文 - 显式传入 tenantId，用于登录等场景
        /// </summary>
        Task<AtlasTenantDbContext> GetReadonlyDbContextAsync(long tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步创建报表库上下文 - 显式传入 tenantId，用于登录等场景
        /// </summary>
        Task<AtlasTenantDbContext> GetReportDbContextAsync(long tenantId, CancellationToken cancellationToken = default);
    }
}