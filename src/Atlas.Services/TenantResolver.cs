using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Atlas.Models.Global.Entities;
using Atlas.Services.Abstractions;
using Microsoft.Extensions.Caching.Memory;

namespace Atlas.Services
{
    public class TenantResolver : ITenantResolver
    {
        private readonly IRepository<Tenant> _tenantRepository;
        private readonly IMemoryCache _cache;

        public TenantResolver(
            IRepository<Tenant> tenantRepository,
            IMemoryCache cache)
        {
            _tenantRepository = tenantRepository;
            _cache = cache;
        }

        public async Task<TenantInfo?> ResolveTenantAsync(long tenantId)
        {
            // 1. 先从缓存获取
            var cacheKey = $"tenant:{tenantId}";
            if (_cache.TryGetValue<TenantInfo>(cacheKey, out var cachedInfo))
                return cachedInfo;

            // 2. 从数据库查询
            var tenant = await _tenantRepository.GetByIdAsync(tenantId);
            if (tenant == null || tenant.Status != TenantStatus.Active)
                return null;

            // 3. 构建租户信息
            var tenantInfo = new TenantInfo
            {
                Id = tenant.Id,
                Name = tenant.Name,
                ConnectionString = tenant.DatabaseInstance.ConnectionString,
                IsActive = true
            };

            // 4. 缓存 1 小时
            _cache.Set(cacheKey, tenantInfo, TimeSpan.FromHours(1));

            return tenantInfo;
        }
    }
}
