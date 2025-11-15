using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant
{
    public interface ITenantDbConnProvider
    {
        long? TenantId { get; }

        /// <summary>
        /// 获取主库连接字符串（读写）
        /// </summary>
        Task<string> GetConnStringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取只读库连接字符串
        /// </summary>
        Task<string> GetReadonlyConnStringAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 获取报表库连接字符串
        /// </summary>
        Task<string> GetReportConnStringAsync(CancellationToken cancellationToken = default);
    }
}
