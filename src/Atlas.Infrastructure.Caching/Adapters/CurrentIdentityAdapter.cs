using Atlas.Core.Services;
using Atlas.Infrastructure.Caching.Keys;

namespace Atlas.Infrastructure.Caching.Adapters;

/// <summary>
/// 当前身份适配器 - 将 ICurrentUserService 适配为 ICurrentIdentity
/// </summary>
public class CurrentIdentityAdapter : ICurrentIdentity
{
    private readonly ICurrentUserService _currentUserService;

    public CurrentIdentityAdapter(ICurrentUserService currentUserService)
    {
        _currentUserService = currentUserService;
    }

    public long? TenantId => _currentUserService?.TenantId;
    public long? StoreId => _currentUserService?.StoreId;
    public long? UserId => _currentUserService?.UserId;
}