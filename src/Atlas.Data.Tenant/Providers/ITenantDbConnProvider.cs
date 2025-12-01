using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Providers
{
    public interface ITenantDbConnProvider
    {
        long? TenantId { get; }

        /// <summary>
        /// 获取主库连接字符串（读写）- 依赖 ICurrentIdentity
        /// </summary>
        Task<string> GetConnStringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取只读库连接字符串 - 依赖 ICurrentIdentity
        /// </summary>
        Task<string> GetReadonlyConnStringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取报表库连接字符串 - 依赖 ICurrentIdentity
        /// </summary>
        Task<string> GetReportConnStringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取主库连接字符串（读写）- 显式传入 tenantId，用于登录等场景
        /// </summary>
        Task<string> GetConnStringAsync(long tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取只读库连接字符串 - 显式传入 tenantId，用于登录等场景
        /// </summary>
        Task<string> GetReadonlyConnStringAsync(long tenantId, CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取报表库连接字符串 - 显式传入 tenantId，用于登录等场景
        /// </summary>
        Task<string> GetReportConnStringAsync(long tenantId, CancellationToken cancellationToken = default);
    }
}
