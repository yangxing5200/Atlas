using Atlas.Core.Services;

namespace Atlas.Data.Tests.Mocks
{
    /// <summary>
    /// Mock的当前用户服务（用于测试）
    /// </summary>
    public class MockCurrentUserService : ICurrentUserService
    {
        public long? UserId { get; set; }
        public string UserName { get; set; }
        public long? TenantId { get; set; }
        public bool IsAuthenticated { get; set; }

        /// <summary>
        /// 创建默认的Mock用户服务
        /// </summary>
        public MockCurrentUserService()
        {
            UserId = 10001;
            UserName = "test_user";
            TenantId = 1;
            IsAuthenticated = true;
        }

        /// <summary>
        /// 创建指定用户ID的Mock服务
        /// </summary>
        public MockCurrentUserService(long userId, long tenantId = 1)
        {
            UserId = userId;
            UserName = $"user_{userId}";
            TenantId = tenantId;
            IsAuthenticated = true;
        }

        /// <summary>
        /// 创建未认证的用户
        /// </summary>
        public static MockCurrentUserService CreateUnauthenticated()
        {
            return new MockCurrentUserService
            {
                UserId = null,
                UserName = null,
                TenantId = null,
                IsAuthenticated = false
            };
        }
    }
}