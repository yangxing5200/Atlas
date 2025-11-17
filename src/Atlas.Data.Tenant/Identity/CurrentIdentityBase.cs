using Atlas.Core.Enums;
using Atlas.Data.Tenant.Repositories;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;

namespace Atlas.Data.Tenant.Identity
{
    /// <summary>
    /// 当前用户服务基类（核心逻辑抽象）
    /// </summary>
    public abstract class CurrentIdentityBase
    {
        protected readonly Lazy<IStoreRepository> _storeRepository;
        protected readonly Lazy<ICacheService> _cache;

        protected static readonly CacheKeyDefinition AccessibleStoresCacheKey =
            CacheKeyDefinition.Create("access:store:{id}")
            .WithInstanceKey("id")
            .WithExpiration(CacheExpirations.TwelveHours)
            .WithScope(CacheScope.Tenant)
            .Build();

        protected CurrentIdentityBase(
            Lazy<IStoreRepository> storeRepository,
            Lazy<ICacheService> cache)
        {
            _storeRepository = storeRepository;
            _cache = cache;
        }

        /// <summary>
        /// 子类需要实现：获取当前门店ID
        /// </summary>
        protected abstract long? GetCurrentStoreId();

        /// <summary>
        /// 获取可访问的门店ID列表（带缓存）
        /// </summary>
        public async Task<List<long>> GetAccessibleStoreIdsAsync(
            CancellationToken ct = default)
        {
            var storeId = GetCurrentStoreId();
            
            if (!storeId.HasValue || _cache==null)
            {
                return new List<long>();
            }

            var result = await _cache.Value.GetOrSetAsync(
                AccessibleStoresCacheKey,
                factory: () => CalculateAccessibleStoreIdsAsync(storeId.Value, ct),
                instanceValue: storeId.Value,
                cancellationToken: ct);

            return result.Value ?? new List<long> { storeId.Value };
        }

        /// <summary>
        /// 计算可访问的门店ID列表
        /// </summary>
        protected async Task<List<long>> CalculateAccessibleStoreIdsAsync(
            long storeId,
            CancellationToken ct)
        {
            var currentStore = await _storeRepository.Value
                .GetByIdAsync(storeId, ct);

            if (currentStore == null)
            {
                return new List<long> { storeId };
            }

            var accessibleStoreIds = new List<long>();

            switch (currentStore.Type)
            {
                case StoreType.Franchised:
                    // 加盟门店：独享
                    accessibleStoreIds.Add(storeId);
                    break;

                case StoreType.Headquarters:
                case StoreType.FranchiseHeadquarters:
                    // 总部：自己 + 下级所有直营门店
                    accessibleStoreIds.Add(storeId);

                    var childDirectStores = await _storeRepository.Value
                        .GetChildDirectStoresAsync(storeId, ct);

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
                        accessibleStoreIds.Add(storeId);
                    }
                    break;

                default:
                    accessibleStoreIds.Add(storeId);
                    break;
            }

            return accessibleStoreIds.Distinct().ToList();
        }
    }
}