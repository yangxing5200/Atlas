using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Providers
{
    /// <summary>
    /// 租户数据库连接信息
    /// </summary>
    public class TenantConnectionInfo
    {
        /// <summary>
        /// 租户ID
        /// </summary>
        public long TenantId { get; set; }

        /// <summary>
        /// 租户名称
        /// </summary>
        public string TenantName { get; set; } = string.Empty;

        /// <summary>
        /// 数据库实例ID
        /// </summary>
        public long DatabaseInstanceId { get; set; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public string DbType { get; set; } = string.Empty;

        /// <summary>
        /// 主数据库Server编码
        /// </summary>
        public string MasterServerCode { get; set; } = string.Empty;

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DbName { get; set; } = string.Empty;

        /// <summary>
        /// 主库连接串(从DatabaseInstance.ConnectionString获取,或从ServerConfig组装)
        /// </summary>
        public string MasterConnectionString { get; set; } = string.Empty;

        /// <summary>
        /// 只读库连接串列表
        /// </summary>
        public List<ReadonlyServerInfo> ReadonlyServers { get; set; } = new();

        /// <summary>
        /// 报表库连接串列表
        /// </summary>
        public List<ReadonlyServerInfo> ReportServers { get; set; } = new();
    }
    /// <summary>
    /// 只读服务器信息
    /// </summary>
    public class ReadonlyServerInfo
    {
        public string ServerCode { get; set; } = string.Empty;
        public string ConnectionString { get; set; } = string.Empty;
        public bool IsReport { get; set; }
        public bool IsPublic { get; set; }
    }
}
