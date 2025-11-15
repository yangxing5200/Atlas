using Atlas.Infrastructure.Caching.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// 租户相关缓存键定义
    /// </summary>
    public static class TenantCacheKeys
    {
        /// <summary>
        /// 租户数据库连接信息缓存
        /// Key: tenant-db-conn:{tenantId}
        /// </summary>
        public static readonly CacheKeyDefinition TenantDbConnection =
            CacheKeyDefinition.Create("tenant-db-conn:{id}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromHours(1))
                .WithDescription("租户数据库连接信息")
                .Build();

        /// <summary>
        /// 数据库服务器配置缓存
        /// Key: db-server-config:{serverCode}:{networkEnvCode}
        /// </summary>
        public static readonly CacheKeyDefinition DatabaseServerConfig =
            CacheKeyDefinition.Create("db-server-config:{key}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("key")
                .WithExpiration(TimeSpan.FromHours(2))
                .WithDescription("数据库服务器配置")
                .Build();
    }
}
