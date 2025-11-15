using Atlas.Core.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Core.Tests.Mocks
{
    /// <summary>
    /// 测试用的当前用户服务
    /// 可以灵活设置不同的租户、门店、用户
    /// </summary>
    public class TestCurrentUserService : ICurrentIdentity
    {
        public long? UserId { get; set; }
        public long? StoreId { get; set; }
        public long? TenantId { get; set; }

        public string UserName => "测试用户";

        public bool IsAuthenticated => true;

        /// <summary>
        /// 默认构造函数 - 使用默认值
        /// </summary>
        public TestCurrentUserService()
        {
            TenantId = 1;
            StoreId = 100;
            UserId = 1000;
        }

        /// <summary>
        /// 自定义构造函数
        /// </summary>
        public TestCurrentUserService(long? tenantId, long? storeId, long? userId)
        {
            TenantId = tenantId;
            StoreId = storeId;
            UserId = userId;
        }

        /// <summary>
        /// 创建租户1的用户
        /// </summary>
        public static TestCurrentUserService CreateTenant1User(long userId = 1000, long storeId = 100)
        {
            return new TestCurrentUserService(tenantId: 1, storeId: storeId, userId: userId);
        }

        /// <summary>
        /// 创建租户2的用户
        /// </summary>
        public static TestCurrentUserService CreateTenant2User(long userId = 2000, long storeId = 200)
        {
            return new TestCurrentUserService(tenantId: 2, storeId: storeId, userId: userId);
        }

        /// <summary>
        /// 创建全局用户（无租户信息）
        /// </summary>
        public static TestCurrentUserService CreateGlobalUser()
        {
            return new TestCurrentUserService(null, null, null);
        }

        public Task<List<long>> GetAccessibleStoreIdsAsync(CancellationToken ct = default)
        {
            throw new NotImplementedException();
        }
    }
}
