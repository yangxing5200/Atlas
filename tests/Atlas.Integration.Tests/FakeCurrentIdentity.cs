using Atlas.Core.Services;
using Atlas.Data.Tenant;
using Atlas.Data.Tenant.Repositories;
using Atlas.Infrastructure.Caching.Abstractions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Integration.Tests
{
    /// <summary>
    /// 可控的身份模拟，用于测试中动态切换租户/用户/门店
    /// </summary>
    /// <summary>
    /// 测试专用的身份实现（支持动态设置身份信息）
    /// </summary>
    public class FakeCurrentIdentity : CurrentIdentityBase, ICurrentIdentity
    {
        private long? _userId;
        private string _userName = string.Empty;
        private long? _storeId;
        private long? _tenantId;
        private bool _isAuthenticated=true;

        public FakeCurrentIdentity(
            Lazy<IStoreRepository> storeRepository,
            Lazy<ICacheService> cache)
            : base(storeRepository, cache)
        {
        }

        // ICurrentIdentity 实现
        public long? UserId => _userId;
        public string UserName => _userName;
        public long? StoreId => _storeId;
        public long? TenantId => _tenantId;
        public bool IsAuthenticated => _isAuthenticated;

        // 基类抽象方法实现
        protected override long? GetCurrentStoreId() => _storeId;

        // ===== 测试辅助方法 =====

        /// <summary>
        /// 设置当前用户信息（链式调用）
        /// </summary>
        public FakeCurrentIdentity SetUser(
            long userId,
            string userName,
            long storeId,
            long tenantId,
            bool isAuthenticated = true)
        {
            _userId = userId;
            _userName = userName;
            _storeId = storeId;
            _tenantId = tenantId;
            _isAuthenticated = isAuthenticated;
            return this;
        }

        /// <summary>
        /// 设置当前门店ID
        /// </summary>
        public FakeCurrentIdentity SetStoreId(long storeId)
        {
            _storeId = storeId;
            return this;
        }

        /// <summary>
        /// 设置当前用户ID
        /// </summary>
        public FakeCurrentIdentity SetUserId(long userId)
        {
            _userId = userId;
            return this;
        }

        /// <summary>
        /// 设置当前租户ID
        /// </summary>
        public FakeCurrentIdentity SetTenantId(long tenantId)
        {
            _tenantId = tenantId;
            return this;
        }

        /// <summary>
        /// 设置用户名
        /// </summary>
        public FakeCurrentIdentity SetUserName(string userName)
        {
            _userName = userName;
            return this;
        }

        /// <summary>
        /// 设置认证状态
        /// </summary>
        public FakeCurrentIdentity SetAuthenticated(bool isAuthenticated)
        {
            _isAuthenticated = isAuthenticated;
            return this;
        }

        public FakeCurrentIdentity SetIdentity(long tenantId,long userId, long? storeId)
        {
            _tenantId = tenantId;
            _storeId = storeId;
            _userId = userId;
            return this;
        }

        /// <summary>
        /// 重置所有信息
        /// </summary>
        public FakeCurrentIdentity Reset()
        {
            _userId = null;
            _userName = string.Empty;
            _storeId = null;
            _tenantId = null;
            _isAuthenticated = false;
            return this;
        }
    }
}
