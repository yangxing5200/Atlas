using System.Reflection;
using Atlas.Core.Authorization;
using Atlas.Core.Entities.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Infrastructure.Security.Permissions;

public sealed class AtlasDataAccessEvaluator : IAtlasDataAccessEvaluator
{
    private readonly IAtlasAuthorizationCatalog _authorizationCatalog;
    private readonly IServiceProvider _serviceProvider;

    public AtlasDataAccessEvaluator(
        IAtlasAuthorizationCatalog authorizationCatalog,
        IServiceProvider serviceProvider)
    {
        _authorizationCatalog = authorizationCatalog ?? throw new ArgumentNullException(nameof(authorizationCatalog));
        _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    public async ValueTask<AtlasDataAccessDecision> CanAccessAsync<TResource>(
        TResource resource,
        AtlasDataAccessContext context,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (resource == null)
            return AtlasDataAccessDecision.Deny("Resource is missing.");

        if (!_authorizationCatalog.DataResources.TryGetValue(context.ResourceCode, out var dataResource))
            return AtlasDataAccessDecision.Deny($"Data resource '{context.ResourceCode}' is not declared.");

        if (!dataResource.SupportedScopes.Contains(context.ScopeType))
            return AtlasDataAccessDecision.Deny($"Data scope '{context.ScopeType}' is not supported by resource '{context.ResourceCode}'.");

        var tenantDecision = EvaluateTenant(resource, context, dataResource);
        if (!tenantDecision.Allowed)
            return tenantDecision;

        var contributor = _serviceProvider.GetService<IAtlasDataScopeContributor<TResource>>();
        AtlasDataAccessDecision? contributorDecision = null;
        if (contributor != null)
        {
            contributorDecision = await contributor.CanAccessAsync(resource, context, ct);
            if (!contributorDecision.Allowed)
                return contributorDecision;
        }

        if (context.ScopeType is AtlasDataScopeType.Department or AtlasDataScopeType.Custom)
        {
            return contributorDecision ??
                   AtlasDataAccessDecision.Deny($"{context.ScopeType} scope requires a resource contributor.");
        }

        return EvaluateBuiltInScope(resource, context, dataResource);
    }

    private static AtlasDataAccessDecision EvaluateTenant<TResource>(
        TResource resource,
        AtlasDataAccessContext context,
        AtlasDataResourceDefinition dataResource)
    {
        var tenantId = TryGetLongProperty(resource, dataResource.TenantField);
        if (!tenantId.HasValue && resource is ITenantEntity tenantEntity)
            tenantId = tenantEntity.TenantId;

        if (!tenantId.HasValue)
            return AtlasDataAccessDecision.Deny($"Tenant field '{dataResource.TenantField}' is missing.");

        return tenantId.Value == context.TenantId
            ? AtlasDataAccessDecision.Allow("Tenant matched.")
            : AtlasDataAccessDecision.Deny("Tenant mismatch.");
    }

    private static AtlasDataAccessDecision EvaluateBuiltInScope<TResource>(
        TResource resource,
        AtlasDataAccessContext context,
        AtlasDataResourceDefinition dataResource)
    {
        return context.ScopeType switch
        {
            AtlasDataScopeType.AllTenant => AtlasDataAccessDecision.Allow("Tenant scope allowed."),
            AtlasDataScopeType.CurrentStore => EvaluateStore(resource, dataResource, context.StoreId),
            AtlasDataScopeType.SharedStores => EvaluateStoreSet(resource, dataResource, context.SharedStoreIds, "Shared store scope allowed."),
            AtlasDataScopeType.AssignedStores => EvaluateStoreSet(resource, dataResource, context.AssignedStoreIds, "Assigned store scope allowed."),
            AtlasDataScopeType.Own => EvaluateOwner(resource, dataResource, context.UserId),
            _ => AtlasDataAccessDecision.Deny($"Unsupported data scope '{context.ScopeType}'.")
        };
    }

    private static AtlasDataAccessDecision EvaluateStore<TResource>(
        TResource resource,
        AtlasDataResourceDefinition dataResource,
        long? expectedStoreId)
    {
        if (!expectedStoreId.HasValue)
            return AtlasDataAccessDecision.Deny("Current store is missing.");

        var storeId = TryGetConfiguredStoreId(resource, dataResource);
        return storeId == expectedStoreId.Value
            ? AtlasDataAccessDecision.Allow("Current store scope allowed.")
            : AtlasDataAccessDecision.Deny("Store mismatch.");
    }

    private static AtlasDataAccessDecision EvaluateStoreSet<TResource>(
        TResource resource,
        AtlasDataResourceDefinition dataResource,
        IReadOnlyCollection<long> storeIds,
        string allowedReason)
    {
        var storeId = TryGetConfiguredStoreId(resource, dataResource);
        return storeId.HasValue && storeIds.Contains(storeId.Value)
            ? AtlasDataAccessDecision.Allow(allowedReason)
            : AtlasDataAccessDecision.Deny("Store is outside the allowed data scope.");
    }

    private static AtlasDataAccessDecision EvaluateOwner<TResource>(
        TResource resource,
        AtlasDataResourceDefinition dataResource,
        long userId)
    {
        if (userId <= 0)
            return AtlasDataAccessDecision.Deny("Current user is missing.");

        if (string.IsNullOrWhiteSpace(dataResource.OwnerField))
            return AtlasDataAccessDecision.Deny($"Owner field is not configured for resource '{dataResource.Code}'.");

        var ownerId = TryGetLongProperty(resource, dataResource.OwnerField);

        return ownerId == userId
            ? AtlasDataAccessDecision.Allow("Owner scope allowed.")
            : AtlasDataAccessDecision.Deny("Owner mismatch.");
    }

    private static long? TryGetConfiguredStoreId<TResource>(
        TResource resource,
        AtlasDataResourceDefinition dataResource)
    {
        return string.IsNullOrWhiteSpace(dataResource.StoreField)
            ? null
            : TryGetLongProperty(resource, dataResource.StoreField);
    }

    private static long? TryGetLongProperty<TResource>(TResource resource, string propertyName)
    {
        var property = typeof(TResource).GetProperty(
            propertyName,
            BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);

        if (property == null)
            return null;

        var value = property.GetValue(resource);
        return value switch
        {
            long longValue => longValue,
            int intValue => intValue,
            _ => null
        };
    }
}
