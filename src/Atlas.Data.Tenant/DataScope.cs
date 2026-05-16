using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.Extensions;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Atlas.Infrastructure.Caching.Abstractions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant
{
    /// <summary>
    /// 数据范围服务 - 基于门店类型控制数据访问权限
    /// </summary>
    public class DataScope : IDataScope
    {
        private readonly ICurrentIdentity _currentIdentity;
        private readonly ITenantDbContextFactory _dbFactory;
        private readonly Lazy<ICacheService> _cache;
        private readonly ILogger<DataScope> _logger;
        private readonly SemaphoreSlim _scopeLock = new(1, 1);
        private DataScopeSnapshot? _resolvedScope;

        /// <summary>
        /// 门店基本信息 DTO
        /// </summary>
        private class StoreInfo
        {
            public long Id { get; set; }
            public StoreType Type { get; set; }
            public long? ParentStoreId { get; set; }
        }

        public DataScope(
            Lazy<ICacheService> cacheService,
            ICurrentIdentity currentIdentity,
            ITenantDbContextFactory dbFactory,
            ILogger<DataScope> logger)
        {
            _cache = cacheService;
            _currentIdentity = currentIdentity;
            _dbFactory = dbFactory;
            _logger = logger;
        }

        public long? TenantId => _currentIdentity?.TenantId;
        public long? StoreId => _currentIdentity?.StoreId;

        public async Task<DataScopeSnapshot> ResolveAsync(CancellationToken ct = default)
        {
            var tenantId = TenantId;
            var storeId = StoreId;

            if (_resolvedScope is { } current &&
                current.TenantId == tenantId &&
                current.StoreId == storeId)
            {
                return current;
            }

            await _scopeLock.WaitAsync(ct);
            try
            {
                if (_resolvedScope is { } cached &&
                    cached.TenantId == tenantId &&
                    cached.StoreId == storeId)
                {
                    return cached;
                }

                var shareStoreIds = storeId.HasValue
                    ? await GetShareStoreIdsAsync(ct)
                    : new List<long>();

                _resolvedScope = new DataScopeSnapshot(
                    tenantId,
                    storeId,
                    shareStoreIds);

                return _resolvedScope;
            }
            finally
            {
                _scopeLock.Release();
            }
        }

        /// <summary>
        /// 预加载门店访问范围到缓存（中间件调用）
        /// </summary>
        public async Task PreloadShareStoreIdsAsync(CancellationToken ct = default)
        {
            _ = await ResolveAsync(ct);
        }

        public async Task<List<long>> GetShareStoreIdsAsync(CancellationToken ct = default)
        {
            var storeId = StoreId;
            if (!storeId.HasValue) return new List<long>();

            var cacheKey = TenantCacheKeys.ShareStoresCacheKey;

            var cachedShareStoreIds = await _cache.Value.GetAsync<List<long>>(
                cacheKey,
                instanceValue: storeId.Value,
                cancellationToken: ct);

            if (cachedShareStoreIds.SafeAny())
            {
                _logger.LogDebug("ShareStoreIds cached, StoreId: {StoreId}", storeId.Value);
                return cachedShareStoreIds!;
            }

            try
            {
                var shareStoreIds = await CalculateShareStoreIdsAsync(storeId.Value, ct);
                await _cache.Value.SetAsync(cacheKey, shareStoreIds, storeId.Value, cancellationToken: ct);

                _logger.LogInformation(
                    "Preloaded ShareStoreIds, StoreId: {StoreId}, Count: {Count}",
                    storeId.Value, shareStoreIds.Count);

                return shareStoreIds;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to preload ShareStoreIds, StoreId: {StoreId}", storeId.Value);
                throw;
            }
        }

        /// <summary>
        /// 计算门店访问范围（绕过Repository层避免循环依赖）
        /// </summary>
        /// <remarks>
        /// 权限规则：
        /// - 加盟店：仅自身
        /// - 总部/加盟总部：自身 + 所有直营子店
        /// - 直营店：父店（总部）+ 所有兄弟直营店
        /// </remarks>
        private async Task<List<long>> CalculateShareStoreIdsAsync(long storeId, CancellationToken ct = default)
        {
            try
            {
                var db = await _dbFactory.GetReadonlyDbContextAsync(ct);
                var tenantId = TenantId;

                if (!tenantId.HasValue)
                {
                    _logger.LogWarning("Missing TenantId, fallback to single store");
                    return new List<long> { storeId };
                }

                // 查询当前门店信息（仅应用租户过滤）
                var currentStore = await db.Set<Store>()
                    .AsNoTracking()
                    .Where(x => x.TenantId == tenantId.Value && x.Id == storeId)
                    .Select(x => new StoreInfo
                    {
                        Id = x.Id,
                        Type = x.Type,
                        ParentStoreId = x.ParentStoreId
                    })
                    .FirstOrDefaultAsync(ct);

                if (currentStore == null)
                {
                    _logger.LogWarning("Store not found, StoreId: {StoreId}", storeId);
                    return new List<long> { storeId };
                }

                var result = await GetStoreIdsByTypeAsync(db, tenantId.Value, currentStore, ct);

                _logger.LogInformation(
                    "Calculated ShareStoreIds, StoreId: {StoreId}, Type: {Type}, Range: [{Ids}]",
                    storeId, currentStore.Type, string.Join(", ", result));

                return result;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to calculate ShareStoreIds, StoreId: {StoreId}", storeId);
                // 异常时返回保守策略
                return new List<long> { storeId };
            }
        }

        /// <summary>
        /// 根据门店类型获取访问范围
        /// </summary>
        private async Task<List<long>> GetStoreIdsByTypeAsync(
            AtlasTenantDbContext db,
            long tenantId,
            StoreInfo currentStore,
            CancellationToken ct)
        {
            var shareStoreIds = new List<long>();

            switch (currentStore.Type)
            {
                case StoreType.Franchised:
                    // 加盟店：仅自身
                    shareStoreIds.Add(currentStore.Id);
                    break;

                case StoreType.Headquarters:
                case StoreType.FranchiseHeadquarters:
                    // 总部：自身 + 直营子店
                    shareStoreIds.Add(currentStore.Id);
                    var childStoreIds = await GetDirectOperatedChildrenAsync(db, tenantId, currentStore.Id, ct);
                    shareStoreIds.AddRange(childStoreIds);
                    break;

                case StoreType.DirectOperated:
                    // 直营店：父店 + 兄弟店
                    if (currentStore.ParentStoreId.HasValue)
                    {
                        shareStoreIds.Add(currentStore.ParentStoreId.Value);
                        var siblingStoreIds = await GetDirectOperatedChildrenAsync(
                            db, tenantId, currentStore.ParentStoreId.Value, ct);
                        shareStoreIds.AddRange(siblingStoreIds);
                    }
                    else
                    {
                        _logger.LogWarning("DirectOperated store missing parent, StoreId: {StoreId}", currentStore.Id);
                        shareStoreIds.Add(currentStore.Id);
                    }
                    break;

                default:
                    _logger.LogWarning("Unknown StoreType: {Type}, StoreId: {StoreId}",
                        currentStore.Type, currentStore.Id);
                    shareStoreIds.Add(currentStore.Id);
                    break;
            }

            return shareStoreIds.Distinct().ToList();
        }

        /// <summary>
        /// 查询直营子店ID列表
        /// </summary>
        private async Task<List<long>> GetDirectOperatedChildrenAsync(
            AtlasTenantDbContext db,
            long tenantId,
            long parentStoreId,
            CancellationToken ct)
        {
            return await db.Set<Store>()
                .AsNoTracking()
                .Where(s => s.TenantId == tenantId
                    && s.ParentStoreId == parentStoreId
                    && s.Type == StoreType.DirectOperated)
                .Select(s => s.Id)
                .ToListAsync(ct);
        }

        /// <summary>
        /// 获取门店访问范围（优先从缓存读取）
        /// </summary>
        /// <remarks>
        /// 兼容旧同步路径。缓存未命中时返回保守策略（仅当前门店）。
        /// 请求处理应优先使用 ResolveAsync 或 GetShareStoreIdsAsync。
        /// </remarks>
        public List<long> GetShareStoreIds()
        {
            var storeId = StoreId;

            if (!storeId.HasValue)
            {
                _logger.LogDebug("Missing StoreId in context");
                return new List<long>();
            }

            if (_cache?.Value == null)
            {
                _logger.LogWarning("Cache service unavailable, StoreId: {StoreId}", storeId.Value);
                return new List<long> { storeId.Value };
            }

            var shareIds = _cache.Value.Get<List<long>>(
                TenantCacheKeys.ShareStoresCacheKey,
                instanceValue: storeId.Value);

            if (shareIds.IsNullOrEmpty())
            {
                _logger.LogWarning(
                    "ShareStoreIds cache miss, StoreId: {StoreId}. " +
                    "Legacy synchronous scope path will use current store only.",
                    storeId.Value);

                return new List<long> { storeId.Value };
            }

            _logger.LogDebug("Cache hit, StoreId: {StoreId}, Count: {Count}",
                storeId.Value, shareIds!.Count);

            return shareIds;
        }
    }
}
