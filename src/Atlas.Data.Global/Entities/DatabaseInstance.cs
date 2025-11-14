using Atlas.Core.Entities;

namespace Atlas.Data.Global.Entities
{
    public class DatabaseInstance:BaseEntity
    {
        /// <summary>
        /// 名称
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public string DbType { get; set; }

        /// <summary>
        /// 主数据库Server编码
        /// </summary>
        public string MasterServerCode { get; set; }

        /// <summary>
        /// 数据库名称
        /// </summary>
        public string DbName { get; set; }

        /// <summary>
        /// 数据库版本
        /// </summary>
        public string Version { get; set; }

        /// <summary>
        /// 所属区域
        /// </summary>
        public string Region { get; set; }

        /// <summary>
        /// 数据库连接串
        /// </summary>
        public string ConnectionString { get; set; }

        public ICollection<Tenant> Tenants { get; set; } = new List<Tenant>();
    }

    /// <summary>
    /// 主数据库Server
    /// </summary>
    public class DatabaseMasterServer : BaseEntity
    {
        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }
    }

    /// <summary>
    /// 只读数据库Server
    /// </summary>
    public class DatabaseReadonlyServer : BaseEntity
    {
        /// <summary>
        /// 编码
        /// </summary>
        public string Code { get; set; }

        /// <summary>
        /// 昵称
        /// </summary>
        public string NickName { get; set; }

        /// <summary>
        /// 主数据库Server编码
        /// </summary>
        public string MasterServerCode { get; set; }

        /// <summary>
        /// 是否是报表只读库
        /// </summary>
        public bool IsReport { get; set; }

        /// <summary>
        /// 是否公开给周边服务访问
        /// </summary>
        public bool IsPublic { get; set; }
    }

    /// <summary>
    /// 数据库Server配置
    /// </summary>
    public class DatabaseServerConfig : BaseEntity
    {
        /// <summary>
        /// Server编码
        /// </summary>
        public string ServerCode { get; set; }

        /// <summary>
        /// 网络环境编码
        /// </summary>
        public string NetworkEnvCode { get; set; }

        /// <summary>
        /// 数据库类型
        /// </summary>
        public string DbType { get; set; }

        /// <summary>
        /// 连接串
        /// </summary>
        public string ConnString { get; set; }
    }

    /// <summary>
    /// 网络环境编码
    /// </summary>
    public class NetworkEnvCodes
    {
        /// <summary>
        /// 默认环境
        /// </summary>
        public static readonly string Default = "default";

        /// <summary>
        /// 经典网络
        /// </summary>
        public static readonly string Classic = "classic";

        /// <summary>
        /// VPC网络
        /// </summary>
        public static readonly string Vpc = "vpc";
    }
}
