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
        /// 异步创建主库上下文（读写）
        /// </summary>
        Task<AtlasTenantDbContext> GetDbContextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步创建只读库上下文
        /// </summary>
        Task<AtlasTenantDbContext> GetReadonlyDbContextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 异步创建报表库上下文
        /// </summary>
        Task<AtlasTenantDbContext> GetReportDbContextAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 同步创建只读库上下文（仅在连接串已缓存时使用）
        /// </summary>
        /// <exception cref="InvalidOperationException">连接串未缓存时抛出</exception>
        AtlasTenantDbContext GetReadonlyDbContext();

        /// <summary>
        /// 同步创建主库上下文（仅在连接串已缓存时使用）
        /// </summary>
        /// <exception cref="InvalidOperationException">连接串未缓存时抛出</exception>
        AtlasTenantDbContext GetDbContext();
    }
}