using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
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

    public ScopeDemoController(
        IScopeDemoService scopeDemoService)
    {
        _scopeDemoService = scopeDemoService ?? throw new ArgumentNullException(nameof(scopeDemoService));
    }

    [HttpGet("visibility")]
    [ProducesResponseType(typeof(ScopeDemoVisibilityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScopeDemoVisibilityResponse>> GetVisibility(CancellationToken ct = default)
    {
        return Ok(await _scopeDemoService.GetVisibilityAsync(ct));
    }
}

public interface IScopeDemoService
{
    Task<ScopeDemoVisibilityResponse> GetVisibilityAsync(CancellationToken ct = default);
}

public sealed class ScopeDemoService : IScopeDemoService
{
    private readonly IDataScope _dataScope;
    private readonly IRepository<Store> _stores;
    private readonly IRepository<Product> _products;
    private readonly IRepository<Inventory> _inventories;

    public ScopeDemoService(
        IDataScope dataScope,
        IRepository<Store> stores,
        IRepository<Product> products,
        IRepository<Inventory> inventories)
    {
        _dataScope = dataScope ?? throw new ArgumentNullException(nameof(dataScope));
        _stores = stores ?? throw new ArgumentNullException(nameof(stores));
        _products = products ?? throw new ArgumentNullException(nameof(products));
        _inventories = inventories ?? throw new ArgumentNullException(nameof(inventories));
    }

    public async Task<ScopeDemoVisibilityResponse> GetVisibilityAsync(CancellationToken ct = default)
    {
        var scope = await _dataScope.ResolveAsync(ct);
        if (!scope.TenantId.HasValue || !scope.StoreId.HasValue)
            throw new InvalidOperationException("Token 中缺少租户或门店上下文。");

        var visibleStoreIds = scope.ShareStoreIds
            .Append(scope.StoreId.Value)
            .Distinct()
            .ToArray();

        var storeQuery = await _stores.QueryAsync(ct);
        var stores = await storeQuery
            .Where(s => visibleStoreIds.Contains(s.Id))
            .OrderBy(s => s.Id)
            .SelectToListAsync(s => new StoreSummary(
                s.Id,
                s.Code,
                s.Name,
                s.Type,
                s.ParentStoreId), ct);

        var storeMap = stores.ToDictionary(s => s.Id);
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
            scope.TenantId.Value,
            currentStore,
            scope.ShareStoreIds,
            ExplainRule(currentStore?.Type),
            products.Select(p => p.WithStoreName(storeMap)).ToList(),
            inventories.Select(i => i.WithStoreName(storeMap)).ToList());
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

public sealed record ScopeDemoVisibilityResponse(
    long TenantId,
    StoreSummary? CurrentStore,
    IReadOnlyList<long> ResolvedSharedStoreIds,
    string ScopeRule,
    IReadOnlyList<VisibleProduct> VisibleSharedProducts,
    IReadOnlyList<VisibleInventory> VisibleStoreOnlyInventories);

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
