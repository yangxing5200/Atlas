using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Services;
using Atlas.Data.Common;
using Atlas.Data.Tenant.Repositories;
using Atlas.Infrastructure.Caching.Abstractions;

namespace Atlas.Data.Tenant.Identity
{
    public class TenantSystemIdentity : CurrentIdentityBase, ICurrentIdentity
    {
        private long? _userId;
        private string _userName = string.Empty;
        private long? _storeId;
        private long? _tenantId;
        private bool _isAuthenticated = true;

        public TenantSystemIdentity(
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
        public TenantSystemIdentity SetUser(SystemIdentity identity)
        {
            _userId = identity.UserId;
            _userName = identity.UserName;
            _storeId = identity.StoreId;
            _tenantId = identity.TenantId;
            _isAuthenticated = identity.IsAuthenticated;
            return this;
        }

        public TenantSystemIdentity SetUser(
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
        public TenantSystemIdentity SetStoreId(long storeId)
        {
            _storeId = storeId;
            return this;
        }

        /// <summary>
        /// 设置当前用户ID
        /// </summary>
        public TenantSystemIdentity SetUserId(long userId)
        {
            _userId = userId;
            return this;
        }

        /// <summary>
        /// 设置当前租户ID
        /// </summary>
        public TenantSystemIdentity SetTenantId(long tenantId)
        {
            _tenantId = tenantId;
            return this;
        }

        /// <summary>
        /// 设置用户名
        /// </summary>
        public TenantSystemIdentity SetUserName(string userName)
        {
            _userName = userName;
            return this;
        }

        /// <summary>
        /// 设置认证状态
        /// </summary>
        public TenantSystemIdentity SetAuthenticated(bool isAuthenticated)
        {
            _isAuthenticated = isAuthenticated;
            return this;
        }

        public TenantSystemIdentity SetIdentity(long tenantId, long userId, long? storeId)
        {
            _tenantId = tenantId;
            _storeId = storeId;
            _userId = userId;
            return this;
        }

        /// <summary>
        /// 重置所有信息
        /// </summary>
        public TenantSystemIdentity Reset()
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
