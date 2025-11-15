using Atlas.Core.Enums;
using Atlas.Core.Services;
using Atlas.Data.Tenant.Repositories;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Microsoft.AspNetCore.Http;
using System.Security.Claims;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// 当前用户服务实现（从HttpContext获取）
    /// </summary>
    public class CurrentIdentity : ICurrentIdentity
    {
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly Lazy<IStoreRepository> _storeRepository;
        private readonly ICacheService _cache;

        private static readonly CacheKeyDefinition AccessibleStoresCacheKey =
            CacheKeyDefinition.Create("access:store:{id}")
            .WithInstanceKey("id")
            .WithExpiration(CacheExpirations.TwelveHours)
            .WithScope( CacheScope.Tenant)
            .Build();

        public CurrentIdentity(
            IHttpContextAccessor httpContextAccessor,
            Lazy<IStoreRepository> storeRepository,
            ICacheService cache)
        {
            _httpContextAccessor = httpContextAccessor;
            _storeRepository = storeRepository;
            _cache = cache;
        }

        private ClaimsPrincipal? User => _httpContextAccessor.HttpContext?.User;

        public long? UserId =>
            long.TryParse(User?.FindFirst("uid")?.Value, out var id)
                ? id : null;

        public string UserName =>
            User?.FindFirst("uname")?.Value ?? string.Empty;

        public long? StoreId =>
            long.TryParse(User?.FindFirst("sid")?.Value, out var id)
                ? id : null;

        public long? TenantId =>
            long.TryParse(User?.FindFirst("tid")?.Value, out var id)
                ? id : null;

        public bool IsAuthenticated =>
            User?.Identity?.IsAuthenticated ?? false;

        /// <summary>
        /// 获取可访问的门店ID列表（带缓存）
        /// </summary>
        public async Task<List<long>> GetAccessibleStoreIdsAsync(
            CancellationToken ct = default)
        {
            if (!StoreId.HasValue)
            {
                return new List<long>();
            }

            var result = await _cache.GetOrSetAsync(
                AccessibleStoresCacheKey,
                factory: () => CalculateAccessibleStoreIdsAsync(ct),
                instanceValue: StoreId.Value,
                cancellationToken: ct);

            return result.Value ?? new List<long> { StoreId.Value };
        }

        private async Task<List<long>> CalculateAccessibleStoreIdsAsync(
            CancellationToken ct)
        {
            if (!StoreId.HasValue)
            {
                return new List<long>();
            }

            var currentStore = await _storeRepository.Value
                .GetByIdAsync(StoreId.Value, ct);

            if (currentStore == null)
            {
                return new List<long> { StoreId.Value };
            }

            var accessibleStoreIds = new List<long>();

            switch (currentStore.Type)
            {
                case StoreType.Franchised:
                    // 加盟门店：独享
                    accessibleStoreIds.Add(StoreId.Value);
                    break;

                case StoreType.Headquarters:
                case StoreType.FranchiseHeadquarters:
                    // 总部：自己 + 下级所有直营门店
                    accessibleStoreIds.Add(StoreId.Value);

                    var childDirectStores = await _storeRepository.Value
                        .GetChildDirectStoresAsync(StoreId.Value, ct);

                    accessibleStoreIds.AddRange(childDirectStores.Select(s => s.Id));
                    break;

                case StoreType.DirectOperated:
                    // 直营门店：同级直营门店 + 上级总部
                    if (currentStore.ParentStoreId.HasValue)
                    {
                        accessibleStoreIds.Add(currentStore.ParentStoreId.Value);

                        var siblingDirectStores = await _storeRepository.Value
                            .GetSiblingDirectStoresAsync(
                                currentStore.ParentStoreId.Value, ct);

                        accessibleStoreIds.AddRange(siblingDirectStores.Select(s => s.Id));
                    }
                    else
                    {
                        accessibleStoreIds.Add(StoreId.Value);
                    }
                    break;

                default:
                    accessibleStoreIds.Add(StoreId.Value);
                    break;
            }

            return accessibleStoreIds.Distinct().ToList();
        }
    }
}