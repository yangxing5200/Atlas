using Atlas.Core.Services;
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
    public class FakeCurrentIdentity : ICurrentIdentity
    {
        private long? _userId;
        private string _userName = "TestUser";
        private long? _storeId;
        private long? _tenantId;
        private Func<CancellationToken, Task<List<long>>>? _accessibleStoreIdsFunc;

        public long? UserId => _userId;
        public string UserName => _userName;
        public long? StoreId => _storeId;
        public long? TenantId => _tenantId;
        public bool IsAuthenticated => _userId.HasValue;

        public FakeCurrentIdentity SetUserId(long? userId)
        {
            _userId = userId;
            return this;
        }

        public FakeCurrentIdentity SetUserName(string userName)
        {
            _userName = userName;
            return this;
        }

        public FakeCurrentIdentity SetStoreId(long? storeId)
        {
            _storeId = storeId;
            return this;
        }

        public FakeCurrentIdentity SetTenantId(long? tenantId)
        {
            _tenantId = tenantId;
            return this;
        }

        public FakeCurrentIdentity SetAccessibleStoreIds(List<long> storeIds)
        {
            _accessibleStoreIdsFunc = _ => Task.FromResult(storeIds);
            return this;
        }

        public FakeCurrentIdentity SetAccessibleStoreIdsFunc(
            Func<CancellationToken, Task<List<long>>> func)
        {
            _accessibleStoreIdsFunc = func;
            return this;
        }

        public Task<List<long>> GetAccessibleStoreIdsAsync(CancellationToken ct = default)
        {
            if (_accessibleStoreIdsFunc != null)
            {
                return _accessibleStoreIdsFunc(ct);
            }

            if (_storeId.HasValue)
            {
                return Task.FromResult(new List<long> { _storeId.Value });
            }

            return Task.FromResult(new List<long>());
        }

        /// <summary>
        /// 重置所有身份信息
        /// </summary>
        public FakeCurrentIdentity Reset()
        {
            _userId = null;
            _userName = "TestUser";
            _storeId = null;
            _tenantId = null;
            _accessibleStoreIdsFunc = null;
            return this;
        }

        /// <summary>
        /// 快速设置完整身份
        /// </summary>
        public FakeCurrentIdentity SetIdentity(long tenantId, long userId, long? storeId = null)
        {
            _tenantId = tenantId;
            _userId = userId;
            _storeId = storeId;
            return this;
        }
    }
}
