using Atlas.Core.Authorization;
using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Infrastructure.Security;
using Atlas.Sample.ECommerce;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/scope-demo")]
[Produces("application/json")]
[Authorize]
public sealed class ScopeDemoController : ControllerBase
{
    private readonly IScopeDemoService _scopeDemoService;

    public ScopeDemoController(IScopeDemoService scopeDemoService)
    {
        _scopeDemoService = scopeDemoService ?? throw new ArgumentNullException(nameof(scopeDemoService));
    }

    [HttpGet("authorization")]
    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
    [ProducesResponseType(typeof(ScopeDemoAuthorizationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScopeDemoAuthorizationResponse>> GetAuthorization(CancellationToken ct = default)
    {
        return Ok(await _scopeDemoService.GetAuthorizationAsync(ct));
    }

    [HttpGet("visibility")]
    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
    [ProducesResponseType(typeof(ScopeDemoVisibilityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScopeDemoVisibilityResponse>> GetVisibility(CancellationToken ct = default)
    {
        return Ok(await _scopeDemoService.GetVisibilityAsync(ct));
    }

    [HttpGet("products")]
    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
    [ProducesResponseType(typeof(ScopeDemoProductsResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ScopeDemoProductsResponse>> GetProducts(
        [FromQuery] AtlasDataScopeType? scopeType,
        CancellationToken ct = default)
    {
        try
        {
            return Ok(await _scopeDemoService.GetProductsAsync(scopeType, ct));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpGet("inventories/current-store")]
    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.InventoriesRead)]
    [ProducesResponseType(typeof(ScopeDemoInventoriesResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScopeDemoInventoriesResponse>> GetCurrentStoreInventories(
        CancellationToken ct = default)
    {
        return Ok(await _scopeDemoService.GetCurrentStoreInventoriesAsync(ct));
    }

    [HttpGet("products/{productId:long}/access")]
    [Authorize(Policy = AuthorizationPolicies.PermissionPrefix + SampleECommercePermissionCodes.ProductsRead)]
    [ProducesResponseType(typeof(ScopeDemoProductAccessResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ScopeDemoProductAccessResponse>> GetProductAccess(
        [FromRoute] long productId,
        [FromQuery] AtlasDataScopeType? scopeType,
        CancellationToken ct = default)
    {
        try
        {
            var result = await _scopeDemoService.GetProductAccessAsync(productId, scopeType, ct);
            return result == null
                ? NotFound(new { message = $"Product '{productId}' not found in current tenant." })
                : Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}

public interface IScopeDemoService
{
    Task<ScopeDemoAuthorizationResponse> GetAuthorizationAsync(CancellationToken ct = default);

    Task<ScopeDemoVisibilityResponse> GetVisibilityAsync(CancellationToken ct = default);

    Task<ScopeDemoProductsResponse> GetProductsAsync(
        AtlasDataScopeType? requestedScopeType = null,
        CancellationToken ct = default);

    Task<ScopeDemoInventoriesResponse> GetCurrentStoreInventoriesAsync(CancellationToken ct = default);

    Task<ScopeDemoProductAccessResponse?> GetProductAccessAsync(
        long productId,
        AtlasDataScopeType? requestedScopeType = null,
        CancellationToken ct = default);
}

public sealed class ScopeDemoService : IScopeDemoService
{
    private static readonly AtlasDataScopeType[] ProductQueryScopes =
    {
        AtlasDataScopeType.CurrentStore,
        AtlasDataScopeType.SharedStores
    };

    private static readonly AtlasDataScopeType[] InventoryQueryScopes =
    {
        AtlasDataScopeType.CurrentStore
    };

    private static readonly IReadOnlyDictionary<long, VisibleProduct> DemoProductsById =
        new Dictionary<long, VisibleProduct>
        {
            [140001] = new(140001, 110001, "总部标准套餐", 199m, false, null),
            [140011] = new(140011, 110011, "直营一店限定套餐", 129m, true, 110001),
            [140012] = new(140012, 110012, "直营二店限定套餐", 139m, true, 110001),
            [140101] = new(140101, 110101, "加盟一店自有套餐", 159m, true, null)
        };

    private readonly IDataScope _dataScope;
    private readonly ICurrentIdentity _currentIdentity;
    private readonly IAtlasAuthorizationCatalog _authorizationCatalog;
    private readonly IAtlasAuthorizationContextService _authorizationContext;
    private readonly IAtlasAuthorizationManagementService _authorizationManagement;
    private readonly IAtlasDataAccessEvaluator _dataAccessEvaluator;
    private readonly IRepository<Store> _stores;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Inventory> _inventories;

    public ScopeDemoService(
        IDataScope dataScope,
        ICurrentIdentity currentIdentity,
        IAtlasAuthorizationCatalog authorizationCatalog,
        IAtlasAuthorizationContextService authorizationContext,
        IAtlasAuthorizationManagementService authorizationManagement,
        IAtlasDataAccessEvaluator dataAccessEvaluator,
        IRepository<Store> stores,
        IRepository<Product> products,
        IRepository<Inventory> inventories)
    {
        _dataScope = dataScope ?? throw new ArgumentNullException(nameof(dataScope));
        _currentIdentity = currentIdentity ?? throw new ArgumentNullException(nameof(currentIdentity));
        _authorizationCatalog = authorizationCatalog ?? throw new ArgumentNullException(nameof(authorizationCatalog));
        _authorizationContext = authorizationContext ?? throw new ArgumentNullException(nameof(authorizationContext));
        _authorizationManagement = authorizationManagement ?? throw new ArgumentNullException(nameof(authorizationManagement));
        _dataAccessEvaluator = dataAccessEvaluator ?? throw new ArgumentNullException(nameof(dataAccessEvaluator));
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
    }

    public async Task<ScopeDemoAuthorizationResponse> GetAuthorizationAsync(CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        var runtimeContext = RequireRuntimeContext(scope);
        var authorization = await _authorizationContext.GetContextAsync(runtimeContext, ct);
        var productRead = await _authorizationContext.ExplainPermissionAsync(
            runtimeContext,
            SampleECommercePermissionCodes.ProductsRead,
            ct);
        var inventoryRead = await _authorizationContext.ExplainPermissionAsync(
            runtimeContext,
            SampleECommercePermissionCodes.InventoriesRead,
            ct);
        var entitlements = await _authorizationManagement.GetTenantEntitlementsAsync(scope.TenantId!.Value, ct);
        var standardPackage = BuildStandardPackageSnapshot();
        var standardCapabilitySet = standardPackage.CapabilityCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var standardPermissionSet = standardPackage.PermissionCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);

        return new ScopeDemoAuthorizationResponse(
            scope.TenantId.Value,
            runtimeContext.UserId,
            scope.StoreId,
            standardPackage,
            entitlements
                .OrderBy(x => x.Id)
                .Select(x => new ScopeDemoEntitlementSnapshot(
                    x.Id,
                    x.SubjectType,
                    x.SubjectId,
                    x.PackageCode,
                    x.CapabilityCode,
                    x.Status))
                .ToArray(),
            authorization.Capabilities
                .Where(standardCapabilitySet.Contains)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            authorization.Permissions
                .Where(standardPermissionSet.Contains)
                .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
                .ToArray(),
            authorization.DataScopes
                .Where(x => standardPermissionSet.Contains(x.PermissionCode))
                .OrderBy(x => x.PermissionCode, StringComparer.OrdinalIgnoreCase)
                .ThenBy(x => x.ScopeType)
                .ToArray(),
            productRead,
            inventoryRead,
            new[]
            {
                "direct_a_mgr: product.read uses SharedStores, so /api/scope-demo/products returns HQ + direct stores.",
                "direct_a_mgr: inventory.read uses CurrentStore, so /api/scope-demo/inventories/current-store returns only direct store inventory.",
                "franchise_mgr: SharedStores resolves to the franchise store only, so shared product scope remains isolated."
            });
    }

    public async Task<ScopeDemoVisibilityResponse> GetVisibilityAsync(CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        var visibleStoreIds = scope.ShareStoreIds
            .Append(scope.StoreId!.Value)
            .Distinct()
            .ToArray();

        var storeMap = await GetStoreMapAsync(visibleStoreIds, ct);
        var currentStore = storeMap.GetValueOrDefault(scope.StoreId.Value);

        var productQuery = await _products.QueryAsync(ct);
        var products = await productQuery
            .OrderBy(p => p.StoreId)
            .SelectToListAsync(p => new VisibleProduct(
                p.Id,
                p.StoreId,
                p.Name,
                p.Price,
                p.IsCustomized,
                p.SourceStoreId), ct);

        var inventoryQuery = await _inventories.QueryAsync(ct);
        var inventories = await inventoryQuery
            .OrderBy(i => i.StoreId)
            .SelectToListAsync(i => new VisibleInventory(
                i.Id,
                i.StoreId,
                i.ProductId,
                i.Quantity,
                i.SafetyStock), ct);

        return new ScopeDemoVisibilityResponse(
            scope.TenantId!.Value,
            currentStore,
            scope.ShareStoreIds,
            ExplainRule(currentStore?.Type),
            products.Select(p => p.WithStoreName(storeMap)).ToList(),
            inventories.Select(i => i.WithStoreName(storeMap)).ToList());
    }

    public async Task<ScopeDemoProductsResponse> GetProductsAsync(
        AtlasDataScopeType? requestedScopeType = null,
        CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        var runtimeContext = RequireRuntimeContext(scope);
        var authorization = await _authorizationContext.GetContextAsync(runtimeContext, ct);
        var appliedScope = ResolveQueryScope(
            authorization,
            SampleECommercePermissionCodes.ProductsRead,
            ProductQueryScopes,
            requestedScopeType);
        var productQuery = await _products.QueryDataScopeAsync(
            SampleECommerceAuthorizationCodes.ProductResource,
            appliedScope,
            ct);
        var products = await productQuery
            .OrderBy(p => p.StoreId)
            .SelectToListAsync(p => new VisibleProduct(
                p.Id,
                p.StoreId,
                p.Name,
                p.Price,
                p.IsCustomized,
                p.SourceStoreId), ct);
        var storeMap = await GetStoreMapAsync(products.Select(x => x.StoreId), ct);

        return new ScopeDemoProductsResponse(
            scope.TenantId!.Value,
            scope.StoreId!.Value,
            requestedScopeType,
            appliedScope,
            GetPermissionDataScopes(authorization, SampleECommercePermissionCodes.ProductsRead),
            scope.ShareStoreIds,
            products.Select(p => p.WithStoreName(storeMap)).ToArray());
    }

    public async Task<ScopeDemoInventoriesResponse> GetCurrentStoreInventoriesAsync(CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        var runtimeContext = RequireRuntimeContext(scope);
        var authorization = await _authorizationContext.GetContextAsync(runtimeContext, ct);
        var appliedScope = ResolveQueryScope(
            authorization,
            SampleECommercePermissionCodes.InventoriesRead,
            InventoryQueryScopes,
            AtlasDataScopeType.CurrentStore);
        var inventoryQuery = await _inventories.QueryDataScopeAsync(
            SampleECommerceAuthorizationCodes.InventoryResource,
            appliedScope,
            ct);
        var inventories = await inventoryQuery
            .OrderBy(i => i.StoreId)
            .SelectToListAsync(i => new VisibleInventory(
                i.Id,
                i.StoreId,
                i.ProductId,
                i.Quantity,
                i.SafetyStock), ct);
        var storeMap = await GetStoreMapAsync(inventories.Select(x => x.StoreId), ct);

        return new ScopeDemoInventoriesResponse(
            scope.TenantId!.Value,
            scope.StoreId!.Value,
            appliedScope,
            GetPermissionDataScopes(authorization, SampleECommercePermissionCodes.InventoriesRead),
            inventories.Select(i => i.WithStoreName(storeMap)).ToArray());
    }

    public async Task<ScopeDemoProductAccessResponse?> GetProductAccessAsync(
        long productId,
        AtlasDataScopeType? requestedScopeType = null,
        CancellationToken ct = default)
    {
        var scope = await RequireScopeAsync(ct);
        var runtimeContext = RequireRuntimeContext(scope);
        var authorization = await _authorizationContext.GetContextAsync(runtimeContext, ct);
        var appliedScope = ResolveQueryScope(
            authorization,
            SampleECommercePermissionCodes.ProductsRead,
            ProductQueryScopes,
            requestedScopeType);

        if (!DemoProductsById.TryGetValue(productId, out var product))
            return null;

        var probe = new ScopeDemoProductAccessProbe(scope.TenantId!.Value, product.StoreId);
        var decision = await _dataAccessEvaluator.CanAccessAsync(
            probe,
            new AtlasDataAccessContext(
                probe.TenantId,
                runtimeContext.UserId,
                scope.StoreId,
                SampleECommerceAuthorizationCodes.ProductResource,
                appliedScope,
                scope.ShareStoreIds,
                scope.ShareStoreIds),
            ct);
        var storeMap = await GetStoreMapAsync(new[] { product.StoreId }, ct);
        var store = storeMap.GetValueOrDefault(product.StoreId);

        return new ScopeDemoProductAccessResponse(
            scope.TenantId.Value,
            runtimeContext.UserId,
            scope.StoreId!.Value,
            requestedScopeType,
            appliedScope,
            GetPermissionDataScopes(authorization, SampleECommercePermissionCodes.ProductsRead),
            product.WithStoreName(storeMap),
            store,
            decision.Allowed,
            decision.Reason,
            scope.ShareStoreIds);
    }

    private async Task<DataScopeSnapshot> RequireScopeAsync(CancellationToken ct)
    {
        var scope = await _dataScope.ResolveAsync(ct);
        if (!scope.TenantId.HasValue || !scope.StoreId.HasValue)
            throw new InvalidOperationException("Token 中缺少租户或门店上下文。");

        return scope;
    }

    private AtlasAuthorizationRuntimeContext RequireRuntimeContext(DataScopeSnapshot scope)
    {
        if (!_currentIdentity.UserId.HasValue)
            throw new InvalidOperationException("Token 中缺少用户上下文。");

        return new AtlasAuthorizationRuntimeContext(
            scope.TenantId!.Value,
            _currentIdentity.UserId.Value,
            scope.StoreId);
    }

    private ScopeDemoPackageSnapshot BuildStandardPackageSnapshot()
    {
        var packageCode = SampleECommerceAuthorizationCodes.StandardPackage;
        _authorizationCatalog.Packages.TryGetValue(packageCode, out var package);
        var capabilityCodes = _authorizationCatalog.PackageCapabilities
            .Where(x => string.Equals(x.PackageCode, packageCode, StringComparison.OrdinalIgnoreCase))
            .Select(x => x.CapabilityCode)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var capabilitySet = capabilityCodes.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var permissionCodes = _authorizationCatalog.Permissions.Values
            .Where(x => capabilitySet.Contains(x.CapabilityCode))
            .Select(x => x.Code)
            .OrderBy(x => x, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        var dataResources = _authorizationCatalog.DataResources.Values
            .Where(x => string.Equals(x.Code, SampleECommerceAuthorizationCodes.ProductResource, StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(x.Code, SampleECommerceAuthorizationCodes.InventoryResource, StringComparison.OrdinalIgnoreCase))
            .OrderBy(x => x.Code, StringComparer.OrdinalIgnoreCase)
            .Select(x => new ScopeDemoDataResourceSnapshot(
                x.Code,
                x.EntityType,
                x.StoreField,
                x.SupportedScopes.ToArray()))
            .ToArray();

        return new ScopeDemoPackageSnapshot(
            packageCode,
            package?.Name ?? packageCode,
            capabilityCodes,
            permissionCodes,
            dataResources);
    }

    private async Task<IReadOnlyDictionary<long, StoreSummary>> GetStoreMapAsync(
        IEnumerable<long> storeIds,
        CancellationToken ct)
    {
        var ids = storeIds.Distinct().ToArray();
        if (ids.Length == 0)
            return new Dictionary<long, StoreSummary>();

        var storeQuery = await _stores.QueryAsync(ct);
        var stores = await storeQuery
            .Where(s => ids.Contains(s.Id))
            .OrderBy(s => s.Id)
            .SelectToListAsync(s => new StoreSummary(
                s.Id,
                s.Code,
                s.Name,
                s.Type,
                s.ParentStoreId), ct);

        return stores.ToDictionary(s => s.Id);
    }

    private static IReadOnlyList<AtlasPermissionScopeGrant> GetPermissionDataScopes(
        AtlasAuthorizationContextSnapshot authorization,
        string permissionCode)
    {
        return authorization.DataScopes
            .Where(x => string.Equals(x.PermissionCode, permissionCode, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(x => RankScope(x.ScopeType))
            .ToArray();
    }

    private static AtlasDataScopeType ResolveQueryScope(
        AtlasAuthorizationContextSnapshot authorization,
        string permissionCode,
        IReadOnlyCollection<AtlasDataScopeType> supportedScopes,
        AtlasDataScopeType? requestedScope)
    {
        var grantedScope = GetPermissionDataScopes(authorization, permissionCode)
            .Select(x => x.ScopeType)
            .DefaultIfEmpty()
            .OrderByDescending(RankScope)
            .FirstOrDefault();

        if (!authorization.Permissions.Contains(permissionCode, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"当前用户未获得权限 '{permissionCode}'。", nameof(permissionCode));

        if (requestedScope.HasValue)
        {
            if (!supportedScopes.Contains(requestedScope.Value))
                throw new ArgumentException(
                    $"资源不支持 dataScope '{requestedScope.Value}'。支持值：{string.Join(", ", supportedScopes)}。",
                    nameof(requestedScope));

            if (!CanUseScope(grantedScope, requestedScope.Value))
                throw new ArgumentException(
                    $"权限 '{permissionCode}' 的授权 dataScope 是 '{grantedScope}'，不能请求更宽的 '{requestedScope.Value}'。",
                    nameof(requestedScope));

            return requestedScope.Value;
        }

        if (supportedScopes.Contains(grantedScope))
            return grantedScope;

        if (grantedScope == AtlasDataScopeType.AllTenant && supportedScopes.Contains(AtlasDataScopeType.SharedStores))
            return AtlasDataScopeType.SharedStores;

        if ((grantedScope is AtlasDataScopeType.AllTenant or AtlasDataScopeType.SharedStores) &&
            supportedScopes.Contains(AtlasDataScopeType.CurrentStore))
        {
            return AtlasDataScopeType.CurrentStore;
        }

        throw new ArgumentException(
            $"权限 '{permissionCode}' 的授权 dataScope '{grantedScope}' 不能应用到当前资源。支持值：{string.Join(", ", supportedScopes)}。",
            nameof(permissionCode));
    }

    private static bool CanUseScope(AtlasDataScopeType grantedScope, AtlasDataScopeType requestedScope)
    {
        return grantedScope switch
        {
            AtlasDataScopeType.AllTenant => true,
            AtlasDataScopeType.SharedStores => requestedScope is AtlasDataScopeType.SharedStores or AtlasDataScopeType.CurrentStore,
            AtlasDataScopeType.AssignedStores => requestedScope is AtlasDataScopeType.AssignedStores or AtlasDataScopeType.CurrentStore,
            _ => grantedScope == requestedScope
        };
    }

    private static int RankScope(AtlasDataScopeType scopeType)
    {
        return scopeType switch
        {
            AtlasDataScopeType.AllTenant => 100,
            AtlasDataScopeType.SharedStores => 70,
            AtlasDataScopeType.AssignedStores => 60,
            AtlasDataScopeType.Department => 50,
            AtlasDataScopeType.CurrentStore => 40,
            AtlasDataScopeType.Own => 30,
            AtlasDataScopeType.Custom => 20,
            _ => 0
        };
    }

    private static string ExplainRule(StoreType? storeType)
    {
        return storeType switch
        {
            StoreType.Headquarters => "总部账号：共享数据可见总部和所有直营子店；门店独享数据只看当前总部。",
            StoreType.DirectOperated => "直营店账号：共享数据可见父总部和所有直营兄弟店；门店独享数据只看当前直营店。",
            StoreType.Franchised => "加盟店账号：共享数据和门店独享数据都只看当前加盟店。",
            StoreType.FranchiseHeadquarters => "加盟总部账号：共享数据可见加盟总部和直营子店；门店独享数据只看当前加盟总部。",
            _ => "未知门店类型。"
        };
    }
}

public sealed record ScopeDemoAuthorizationResponse(
    long TenantId,
    long UserId,
    long? StoreId,
    ScopeDemoPackageSnapshot StandardPackage,
    IReadOnlyList<ScopeDemoEntitlementSnapshot> TenantEntitlements,
    IReadOnlyList<string> RuntimeStandardCapabilities,
    IReadOnlyList<string> RuntimeStandardPermissions,
    IReadOnlyList<AtlasPermissionScopeGrant> RuntimeStandardDataScopes,
    AtlasPermissionExplanation ProductReadExplanation,
    AtlasPermissionExplanation InventoryReadExplanation,
    IReadOnlyList<string> DemoScenarios);

public sealed record ScopeDemoPackageSnapshot(
    string Code,
    string Name,
    IReadOnlyList<string> CapabilityCodes,
    IReadOnlyList<string> PermissionCodes,
    IReadOnlyList<ScopeDemoDataResourceSnapshot> DataResources);

public sealed record ScopeDemoDataResourceSnapshot(
    string Code,
    string? EntityType,
    string? StoreField,
    IReadOnlyList<AtlasDataScopeType> SupportedScopes);

public sealed record ScopeDemoEntitlementSnapshot(
    long Id,
    AtlasEntitlementSubjectType SubjectType,
    long SubjectId,
    string? PackageCode,
    string? CapabilityCode,
    AtlasEntitlementStatus Status);

public sealed record ScopeDemoProductAccessProbe(
    long TenantId,
    long StoreId);

public sealed record ScopeDemoVisibilityResponse(
    long TenantId,
    StoreSummary? CurrentStore,
    IReadOnlyList<long> ResolvedSharedStoreIds,
    string ScopeRule,
    IReadOnlyList<VisibleProduct> VisibleSharedProducts,
    IReadOnlyList<VisibleInventory> VisibleStoreOnlyInventories);

public sealed record ScopeDemoProductsResponse(
    long TenantId,
    long StoreId,
    AtlasDataScopeType? RequestedScope,
    AtlasDataScopeType AppliedScope,
    IReadOnlyList<AtlasPermissionScopeGrant> PermissionDataScopes,
    IReadOnlyList<long> ResolvedSharedStoreIds,
    IReadOnlyList<VisibleProduct> Products);

public sealed record ScopeDemoInventoriesResponse(
    long TenantId,
    long StoreId,
    AtlasDataScopeType AppliedScope,
    IReadOnlyList<AtlasPermissionScopeGrant> PermissionDataScopes,
    IReadOnlyList<VisibleInventory> Inventories);

public sealed record ScopeDemoProductAccessResponse(
    long TenantId,
    long UserId,
    long StoreId,
    AtlasDataScopeType? RequestedScope,
    AtlasDataScopeType AppliedScope,
    IReadOnlyList<AtlasPermissionScopeGrant> PermissionDataScopes,
    VisibleProduct Product,
    StoreSummary? ProductStore,
    bool Allowed,
    string Reason,
    IReadOnlyList<long> ResolvedSharedStoreIds);

public sealed record StoreSummary(
    long Id,
    string Code,
    string Name,
    StoreType Type,
    long? ParentStoreId);

public sealed record VisibleProduct(
    long Id,
    long StoreId,
    string Name,
    decimal Price,
    bool IsCustomized,
    long? SourceStoreId)
{
    public string? StoreName { get; init; }
    public StoreType? StoreType { get; init; }

    public VisibleProduct WithStoreName(IReadOnlyDictionary<long, StoreSummary> stores)
    {
        return stores.TryGetValue(StoreId, out var store)
            ? this with { StoreName = store.Name, StoreType = store.Type }
            : this;
    }
}

public sealed record VisibleInventory(
    long Id,
    long StoreId,
    long ProductId,
    int Quantity,
    int SafetyStock)
{
    public string? StoreName { get; init; }
    public StoreType? StoreType { get; init; }

    public VisibleInventory WithStoreName(IReadOnlyDictionary<long, StoreSummary> stores)
    {
        return stores.TryGetValue(StoreId, out var store)
            ? this with { StoreName = store.Name, StoreType = store.Type }
            : this;
    }
}
