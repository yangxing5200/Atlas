using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using static System.Formats.Asn1.AsnWriter;

namespace Atlas.Data.Common
{
    /// <summary>
    /// 门店服务实现
    /// </summary>
    public class StoreService : IStoreService
    {
        private readonly DbContext _context;
        private readonly IMemoryCache _cache;
        private const string CACHE_KEY_PREFIX = "Store_ShareIds_";
        private const int CACHE_MINUTES = 30;

        public StoreService(DbContext context, IMemoryCache cache)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        /// <summary>
        /// 获取门店共享范围ID列表（带缓存）
        /// </summary>
        public List<long> GetShareStoreIds(long storeId)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{storeId}";

            return _cache.GetOrCreate(cacheKey, entry =>
            {
                entry.AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(CACHE_MINUTES);
                return CalculateShareStoreIds(storeId);
            });
        }

        /// <summary>
        /// 计算门店共享范围
        /// </summary>
        private List<long> CalculateShareStoreIds(long currentStoreId)
        {
            var currentStore = _context.Set<Store>()
                .AsNoTracking()
                .FirstOrDefault(s => s.Id == currentStoreId);

            if (currentStore == null)
            {
                return new List<long> { currentStoreId };
            }

            var shareStoreIds = new List<long>();

            switch (currentStore.Type)
            {
                case StoreType.Franchised:
                    // 加盟门店：独享
                    shareStoreIds.Add(currentStoreId);
                    break;

                case StoreType.Headquarters:
                case StoreType.FranchiseHeadquarters:
                    // 总部：自己 + 下级所有直营门店
                    shareStoreIds.Add(currentStoreId);

                    var childDirectStores = _context.Set<Store>()
                        .AsNoTracking()
                        .Where(s => s.ParentStoreId == currentStoreId
                                 && s.Type == StoreType.DirectOperated)
                        .Select(s => s.Id)
                        .ToList();

                    shareStoreIds.AddRange(childDirectStores);
                    break;

                case StoreType.DirectOperated:
                    // 直营门店：同级直营门店 + 上级总部
                    if (currentStore.ParentStoreId.HasValue)
                    {
                        // 添加上级总部
                        shareStoreIds.Add(currentStore.ParentStoreId.Value);

                        // 添加同级直营门店（包括自己）
                        var siblingDirectStores = _context.Set<Store>()
                            .AsNoTracking()
                            .Where(s => s.ParentStoreId == currentStore.ParentStoreId
                                     && s.Type == StoreType.DirectOperated)
                            .Select(s => s.Id)
                            .ToList();

                        shareStoreIds.AddRange(siblingDirectStores);
                    }
                    else
                    {
                        // 没有上级，只能看到自己
                        shareStoreIds.Add(currentStoreId);
                    }
                    break;

                default:
                    shareStoreIds.Add(currentStoreId);
                    break;
            }

            return shareStoreIds.Distinct().ToList();
        }

        /// <summary>
        /// 获取门店信息
        /// </summary>
        public async Task<Store> GetStoreAsync(long storeId)
        {
            return await _context.Set<Store>()
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == storeId);
        }

        /// <summary>
        /// 刷新门店缓存（门店信息变更时调用）
        /// </summary>
        public void RefreshCache(long storeId)
        {
            var cacheKey = $"{CACHE_KEY_PREFIX}{storeId}";
            _cache.Remove(cacheKey);

            // 如果是总部或直营门店，还需要刷新相关门店的缓存
            var store = _context.Set<Store>()
                .AsNoTracking()
                .FirstOrDefault(s => s.Id == storeId);

            if (store == null) return;

            // 刷新父门店缓存
            if (store.ParentStoreId.HasValue)
            {
                _cache.Remove($"{CACHE_KEY_PREFIX}{store.ParentStoreId.Value}");
            }

            // 如果是总部，刷新所有子门店缓存
            if (store.Type == StoreType.Headquarters || store.Type == StoreType.FranchiseHeadquarters)
            {
                var childStoreIds = _context.Set<Store>()
                    .AsNoTracking()
                    .Where(s => s.ParentStoreId == storeId)
                    .Select(s => s.Id)
                    .ToList();

                foreach (var childId in childStoreIds)
                {
                    _cache.Remove($"{CACHE_KEY_PREFIX}{childId}");
                }
            }

            // 如果是直营门店，刷新同级门店缓存
            if (store.Type == StoreType.DirectOperated && store.ParentStoreId.HasValue)
            {
                var siblingStoreIds = _context.Set<Store>()
                    .AsNoTracking()
                    .Where(s => s.ParentStoreId == store.ParentStoreId
                             && s.Type == StoreType.DirectOperated
                             && s.Id != storeId)
                    .Select(s => s.Id)
                    .ToList();

                foreach (var siblingId in siblingStoreIds)
                {
                    _cache.Remove($"{CACHE_KEY_PREFIX}{siblingId}");
                }
            }
        }
    }
}
