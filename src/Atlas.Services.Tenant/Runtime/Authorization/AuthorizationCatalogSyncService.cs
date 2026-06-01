using System.Text.Json;
using Atlas.Core.Authorization;
using Atlas.Core.Entities.Global;
using Atlas.Data.Global;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Services.Tenant.Runtime.Authorization;

public sealed class AuthorizationCatalogSyncService : IAuthorizationCatalogSyncService
{
    private readonly AtlasGlobalDbContext _globalDbContext;
    private readonly IAtlasAuthorizationCatalog _authorizationCatalog;

    public AuthorizationCatalogSyncService(
        AtlasGlobalDbContext globalDbContext,
        IAtlasAuthorizationCatalog authorizationCatalog)
    {
        _globalDbContext = globalDbContext ?? throw new ArgumentNullException(nameof(globalDbContext));
        _authorizationCatalog = authorizationCatalog ?? throw new ArgumentNullException(nameof(authorizationCatalog));
    }

    public async Task SyncAsync(CancellationToken ct = default)
    {
        await SyncCapabilitiesAsync(ct);
        await SyncPackagesAsync(ct);
        await SyncPackageCapabilitiesAsync(ct);
        await SyncMenuItemsAsync(ct);
        await _globalDbContext.SaveChangesAsync(ct);
    }

    private async Task SyncCapabilitiesAsync(CancellationToken ct)
    {
        var existing = await _globalDbContext.Capabilities.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var definition in _authorizationCatalog.Capabilities.Values)
        {
            if (!existing.TryGetValue(definition.Code, out var entity))
            {
                entity = new Capability { Code = definition.Code, CreatedAt = DateTime.UtcNow };
                await _globalDbContext.Capabilities.AddAsync(entity, ct);
            }

            entity.Name = definition.Name;
            entity.Category = definition.Category;
            entity.Description = definition.Description;
            entity.IsEnabled = definition.IsEnabled;
            entity.SourceModule = definition.SourceModule;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task SyncPackagesAsync(CancellationToken ct)
    {
        var existing = await _globalDbContext.FeaturePackages.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var definition in _authorizationCatalog.Packages.Values)
        {
            if (!existing.TryGetValue(definition.Code, out var entity))
            {
                entity = new FeaturePackage { Code = definition.Code, CreatedAt = DateTime.UtcNow };
                await _globalDbContext.FeaturePackages.AddAsync(entity, ct);
            }

            entity.Name = definition.Name;
            entity.Type = definition.Type;
            entity.Description = definition.Description;
            entity.IsEnabled = definition.IsEnabled;
            entity.SourceModule = definition.SourceModule;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task SyncPackageCapabilitiesAsync(CancellationToken ct)
    {
        var existing = await _globalDbContext.PackageCapabilities
            .ToDictionaryAsync(x => $"{x.PackageCode}:{x.CapabilityCode}", StringComparer.OrdinalIgnoreCase, ct);

        foreach (var definition in _authorizationCatalog.PackageCapabilities)
        {
            var key = $"{definition.PackageCode}:{definition.CapabilityCode}";
            if (!existing.TryGetValue(key, out var entity))
            {
                entity = new PackageCapability
                {
                    PackageCode = definition.PackageCode,
                    CapabilityCode = definition.CapabilityCode,
                    CreatedAt = DateTime.UtcNow
                };
                await _globalDbContext.PackageCapabilities.AddAsync(entity, ct);
            }

            entity.LimitJson = definition.LimitJson;
            entity.OptionJson = definition.OptionJson;
            entity.SourceModule = definition.SourceModule;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }

    private async Task SyncMenuItemsAsync(CancellationToken ct)
    {
        var existing = await _globalDbContext.MenuItems.ToDictionaryAsync(x => x.Code, StringComparer.OrdinalIgnoreCase, ct);
        foreach (var definition in _authorizationCatalog.MenuItems.Values)
        {
            if (!existing.TryGetValue(definition.Code, out var entity))
            {
                entity = new MenuItem { Code = definition.Code, CreatedAt = DateTime.UtcNow };
                await _globalDbContext.MenuItems.AddAsync(entity, ct);
            }

            entity.Name = definition.Name;
            entity.Route = definition.Route;
            entity.ParentCode = definition.ParentCode;
            entity.Icon = definition.Icon;
            entity.SortOrder = definition.SortOrder;
            entity.VisibleWhenJson = definition.VisibleWhen == null
                ? null
                : JsonSerializer.Serialize(definition.VisibleWhen);
            entity.IsEnabled = definition.IsEnabled;
            entity.SourceModule = definition.SourceModule;
            entity.UpdatedAt = DateTime.UtcNow;
        }
    }
}
