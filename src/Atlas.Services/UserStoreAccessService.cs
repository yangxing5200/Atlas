using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Repositories;
using Atlas.Models.DTOs;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Services;

public sealed record UserStoreAccess(UserStore UserStore, Store Store);

public interface IUserStoreAccessService
{
    Task<List<UserStoreAccess>> GetAccessibleStoresAsync(long userId, long tenantId);

    Store? DetermineLoginStore(User user, IReadOnlyCollection<UserStoreAccess> accessibleStores, long? requestedStoreId);

    StoreInfoDto MapToStoreInfo(Store store, IReadOnlyCollection<UserStoreAccess> accessibleStores);
}

public sealed class UserStoreAccessService : IUserStoreAccessService
{
    private readonly IRepository<UserStore> _userStoreRepository;
    private readonly IStoreRepository _storeRepository;

    public UserStoreAccessService(
        IRepository<UserStore> userStoreRepository,
        IStoreRepository storeRepository)
    {
        _userStoreRepository = userStoreRepository;
        _storeRepository = storeRepository;
    }

    public async Task<List<UserStoreAccess>> GetAccessibleStoresAsync(long userId, long tenantId)
    {
        var userStoreQueryBuilder = await _userStoreRepository.QueryAsync(tenantId);
        var now = DateTime.UtcNow;

        var userStores = await userStoreQueryBuilder
            .Where(us => us.UserId == userId
                         && (us.EffectiveFrom == null || us.EffectiveFrom <= now)
                         && (us.EffectiveTo == null || us.EffectiveTo >= now))
            .ToListAsync();

        if (!userStores.Any())
        {
            return new List<UserStoreAccess>();
        }

        var storeIds = userStores.Select(us => us.StoreId).ToList();
        var storeQueryBuilder = await _storeRepository.QueryAsync(tenantId);
        var stores = await storeQueryBuilder
            .Where(s => storeIds.Contains(s.Id) && s.IsActive)
            .ToListAsync();

        return userStores
            .Join(stores, us => us.StoreId, s => s.Id, (us, s) => new UserStoreAccess(us, s))
            .ToList();
    }

    public Store? DetermineLoginStore(User user, IReadOnlyCollection<UserStoreAccess> accessibleStores, long? requestedStoreId)
    {
        if (requestedStoreId.HasValue)
        {
            return accessibleStores.FirstOrDefault(s => s.Store.Id == requestedStoreId.Value)?.Store;
        }

        var primary = accessibleStores.FirstOrDefault(s => s.UserStore.IsPrimary);
        if (primary?.Store != null)
        {
            return primary.Store;
        }

        if (user.DefaultStoreId.HasValue)
        {
            var defaultStore = accessibleStores.FirstOrDefault(s => s.Store.Id == user.DefaultStoreId.Value);
            if (defaultStore?.Store != null)
            {
                return defaultStore.Store;
            }
        }

        return accessibleStores.FirstOrDefault()?.Store;
    }

    public StoreInfoDto MapToStoreInfo(Store store, IReadOnlyCollection<UserStoreAccess> accessibleStores)
    {
        var userStore = accessibleStores.FirstOrDefault(s => s.Store.Id == store.Id)?.UserStore;
        return new StoreInfoDto
        {
            Id = store.Id,
            Code = store.Code,
            Name = store.Name,
            Type = (int)store.Type,
            TypeName = store.Type.ToString(),
            ParentStoreId = store.ParentStoreId,
            IsPrimary = userStore?.IsPrimary ?? false
        };
    }
}
