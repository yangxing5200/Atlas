using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Atlas.Data.Abstractions;
using Atlas.Data.Tenant.Context;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Sample.WebApi.Controllers;

[ApiController]
[Route("api/scope-demo")]
[Produces("application/json")]
[Authorize]
public sealed class ScopeDemoController : ControllerBase
{
    private readonly ITenantDbContextFactory _dbFactory;
    private readonly IDataScope _dataScope;
    private readonly ILogger<ScopeDemoController> _logger;

    public ScopeDemoController(
        ITenantDbContextFactory dbFactory,
        IDataScope dataScope,
        ILogger<ScopeDemoController> logger)
    {
        _dbFactory = dbFactory;
        _dataScope = dataScope;
        _logger = logger;
    }

    [HttpGet("visibility")]
    [ProducesResponseType(typeof(ScopeDemoVisibilityResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ScopeDemoVisibilityResponse>> GetVisibility(CancellationToken ct = default)
    {
        try
        {
            var scope = await _dataScope.ResolveAsync(ct);
            if (!scope.TenantId.HasValue || !scope.StoreId.HasValue)
            {
                return BadRequest(new { message = "Token 中缺少租户或门店上下文。" });
            }

            var db = await _dbFactory.GetReadonlyDbContextAsync(ct);
            var stores = await db.Set<Store>()
                .AsNoTracking()
                .Where(s => s.TenantId == scope.TenantId.Value)
                .OrderBy(s => s.Id)
                .Select(s => new StoreSummary(
                    s.Id,
                    s.Code,
                    s.Name,
                    s.Type,
                    s.ParentStoreId))
                .ToListAsync(ct);

            var storeMap = stores.ToDictionary(s => s.Id);
            var currentStore = storeMap.GetValueOrDefault(scope.StoreId.Value);

            var products = await db.ScopedSet<Product>(scope)
                .AsNoTracking()
                .OrderBy(p => p.StoreId)
                .ThenBy(p => p.Id)
                .Select(p => new VisibleProduct(
                    p.Id,
                    p.StoreId,
                    p.Name,
                    p.Price,
                    p.IsCustomized,
                    p.SourceStoreId))
                .ToListAsync(ct);

            var inventories = await db.ScopedSet<Inventory>(scope)
                .AsNoTracking()
                .OrderBy(i => i.StoreId)
                .ThenBy(i => i.Id)
                .Select(i => new VisibleInventory(
                    i.Id,
                    i.StoreId,
                    i.ProductId,
                    i.Quantity,
                    i.SafetyStock))
                .ToListAsync(ct);

            return Ok(new ScopeDemoVisibilityResponse(
                scope.TenantId.Value,
                currentStore,
                scope.ShareStoreIds,
                ExplainRule(currentStore?.Type),
                products.Select(p => p.WithStoreName(storeMap)).ToList(),
                inventories.Select(i => i.WithStoreName(storeMap)).ToList()));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load scope demo visibility.");
            return StatusCode(500, new { message = "读取数据范围演示数据失败。" });
        }
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
