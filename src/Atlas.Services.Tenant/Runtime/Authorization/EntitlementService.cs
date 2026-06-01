using Atlas.Core.Authorization;
using Atlas.Core.Entities.Global;
using Atlas.Data.Global;
using Atlas.Infrastructure.Caching.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Services.Tenant.Runtime.Authorization;

public sealed class EntitlementService : IEntitlementService
{
    private static readonly TimeSpan CacheExpiration = TimeSpan.FromMinutes(5);
    private static readonly TimeSpan VersionCacheExpiration = TimeSpan.FromHours(24);
    private readonly AtlasGlobalDbContext _globalDbContext;
    private readonly IAtlasAuthorizationCatalog _authorizationCatalog;
    private readonly ICacheService _cache;

    public EntitlementService(
        AtlasGlobalDbContext globalDbContext,
        IAtlasAuthorizationCatalog authorizationCatalog,
        ICacheService cache)
    {
        _globalDbContext = globalDbContext ?? throw new ArgumentNullException(nameof(globalDbContext));
        _authorizationCatalog = authorizationCatalog ?? throw new ArgumentNullException(nameof(authorizationCatalog));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
    }

    public async Task<IReadOnlySet<string>> GetAvailableCapabilitiesAsync(
        EntitlementCheckContext context,
        CancellationToken ct = default)
    {
        if (context.TenantId <= 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var version = GetEntitlementCacheVersion(context.TenantId, context.StoreId);
        var cacheKey = BuildCapabilityCacheKey(context.TenantId, context.StoreId, version);
        var cached = _cache.Get<string[]>(cacheKey);
        if (cached != null)
            return cached.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var now = context.UtcNow ?? DateTime.UtcNow;
        var entitlements = await QueryActiveEntitlements(context, now, ct);

        var capabilities = entitlements.Count == 0
            ? GetAllCatalogCapabilities()
            : ExpandEntitlementsToCapabilities(entitlements);

        var result = capabilities
            .Where(code => _authorizationCatalog.Capabilities.TryGetValue(code, out var capability) && capability.IsEnabled)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _cache.Set(cacheKey, result, CacheExpiration);
        return result.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<IReadOnlySet<string>> GetAvailablePermissionsAsync(
        EntitlementCheckContext context,
        CancellationToken ct = default)
    {
        var version = GetEntitlementCacheVersion(context.TenantId, context.StoreId);
        var cacheKey = BuildPermissionCacheKey(context.TenantId, context.StoreId, version);
        var cached = _cache.Get<string[]>(cacheKey);
        if (cached != null)
            return cached.ToHashSet(StringComparer.OrdinalIgnoreCase);

        var availableCapabilities = await GetAvailableCapabilitiesAsync(context, ct);
        var permissions = _authorizationCatalog.Permissions.Values
            .Where(permission => permission.IsEnabled &&
                                 availableCapabilities.Contains(permission.CapabilityCode))
            .Select(permission => NormalizeCode(permission.Code))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        _cache.Set(cacheKey, permissions, CacheExpiration);
        return permissions.ToHashSet(StringComparer.OrdinalIgnoreCase);
    }

    public async Task<bool> IsPermissionAvailableAsync(
        EntitlementCheckContext context,
        string permissionCode,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(permissionCode))
            return false;

        var permissions = await GetAvailablePermissionsAsync(context, ct);
        return permissions.Contains(NormalizeCode(permissionCode));
    }

    public Task InvalidateEntitlementsAsync(
        long tenantId,
        long? storeId = null,
        CancellationToken ct = default)
    {
        if (tenantId <= 0)
            return Task.CompletedTask;

        if (storeId.HasValue)
            BumpVersion(BuildStoreVersionCacheKey(tenantId, storeId.Value));
        else
            BumpVersion(BuildTenantVersionCacheKey(tenantId));

        _cache.Remove(BuildLegacyCapabilityCacheKey(tenantId, storeId));
        _cache.Remove(BuildLegacyPermissionCacheKey(tenantId, storeId));
        return Task.CompletedTask;
    }

    private async Task<List<TenantEntitlement>> QueryActiveEntitlements(
        EntitlementCheckContext context,
        DateTime utcNow,
        CancellationToken ct)
    {
        return await _globalDbContext.TenantEntitlements
            .AsNoTracking()
            .Where(x => x.TenantId == context.TenantId &&
                        x.Status == AtlasEntitlementStatus.Active &&
                        x.StartAtUtc <= utcNow &&
                        (!x.EndAtUtc.HasValue || x.EndAtUtc.Value > utcNow) &&
                        (
                            x.SubjectType == AtlasEntitlementSubjectType.Tenant && x.SubjectId == context.TenantId ||
                            context.StoreId.HasValue &&
                            x.SubjectType == AtlasEntitlementSubjectType.Store && x.SubjectId == context.StoreId.Value
                        ))
            .ToListAsync(ct);
    }

    private IReadOnlyCollection<string> GetAllCatalogCapabilities()
    {
        return _authorizationCatalog.Capabilities.Values
            .Where(capability => capability.IsEnabled)
            .Select(capability => capability.Code)
            .ToArray();
    }

    private IReadOnlyCollection<string> ExpandEntitlementsToCapabilities(
        IReadOnlyCollection<TenantEntitlement> entitlements)
    {
        var capabilities = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var entitlement in entitlements)
        {
            if (!string.IsNullOrWhiteSpace(entitlement.CapabilityCode))
                capabilities.Add(NormalizeCode(entitlement.CapabilityCode));

            if (string.IsNullOrWhiteSpace(entitlement.PackageCode))
                continue;

            var packageCode = NormalizeCode(entitlement.PackageCode);
            foreach (var packageCapability in _authorizationCatalog.PackageCapabilities)
            {
                if (string.Equals(packageCapability.PackageCode, packageCode, StringComparison.OrdinalIgnoreCase))
                    capabilities.Add(packageCapability.CapabilityCode);
            }
        }

        return capabilities;
    }

    private string GetEntitlementCacheVersion(long tenantId, long? storeId)
    {
        var tenantVersion = _cache.Get<string>(BuildTenantVersionCacheKey(tenantId));
        tenantVersion = string.IsNullOrWhiteSpace(tenantVersion) ? "0" : tenantVersion;

        if (!storeId.HasValue)
            return tenantVersion;

        var storeVersion = _cache.Get<string>(BuildStoreVersionCacheKey(tenantId, storeId.Value));
        storeVersion = string.IsNullOrWhiteSpace(storeVersion) ? "0" : storeVersion;
        return $"{tenantVersion}:{storeVersion}";
    }

    private void BumpVersion(string key)
    {
        _cache.Remove(key);
        _cache.Set(key, Guid.NewGuid().ToString("N"), VersionCacheExpiration);
    }

    private static string BuildCapabilityCacheKey(long tenantId, long? storeId, string version)
    {
        return $"entitlements:capabilities:{tenantId}:{storeId.GetValueOrDefault()}:{version}";
    }

    private static string BuildPermissionCacheKey(long tenantId, long? storeId, string version)
    {
        return $"entitlements:permissions:{tenantId}:{storeId.GetValueOrDefault()}:{version}";
    }

    private static string BuildLegacyCapabilityCacheKey(long tenantId, long? storeId)
    {
        return $"entitlements:capabilities:{tenantId}:{storeId.GetValueOrDefault()}";
    }

    private static string BuildLegacyPermissionCacheKey(long tenantId, long? storeId)
    {
        return $"entitlements:permissions:{tenantId}:{storeId.GetValueOrDefault()}";
    }

    private static string BuildTenantVersionCacheKey(long tenantId)
    {
        return $"entitlements:version:{tenantId}:tenant";
    }

    private static string BuildStoreVersionCacheKey(long tenantId, long storeId)
    {
        return $"entitlements:version:{tenantId}:store:{storeId}";
    }

    private static string NormalizeCode(string code)
    {
        return code.Trim().ToLowerInvariant();
    }
}
