using Atlas.Core.Entities.Tenant;
using Atlas.Data.Abstractions;
using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Requests;
using Atlas.Models.Tenant.Responses;
using Atlas.Services.Abstractions.Queries;
using Atlas.Core.Authorization;

namespace Atlas.Services.Queries;

public sealed class ProductQueryService : IProductQueryService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;
    private readonly IRepository<Product> _products;

    public ProductQueryService(IRepository<Product> products)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
    }

    public async Task<PagedResult<ProductDto>> SearchAsync(ProductSearchQuery query, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        if (query.MinPrice.HasValue && query.MaxPrice.HasValue && query.MinPrice.Value > query.MaxPrice.Value)
            throw new ArgumentException("MinPrice cannot be greater than MaxPrice.", nameof(query));

        var pageIndex = query.PageIndex < 1 ? 1 : query.PageIndex;
        var pageSize = query.PageSize < 1 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);

        var builder = await _products.QueryDataScopeAsync("product", AtlasDataScopeType.SharedStores, ct);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(product =>
                product.Name.Contains(keyword) ||
                product.Description.Contains(keyword));
        }

        if (query.StoreId.HasValue)
            builder = builder.Where(product => product.StoreId == query.StoreId.Value);

        if (query.MinPrice.HasValue)
            builder = builder.Where(product => product.Price >= query.MinPrice.Value);

        if (query.MaxPrice.HasValue)
            builder = builder.Where(product => product.Price <= query.MaxPrice.Value);

        if (query.OnlyCustomized.HasValue)
            builder = builder.Where(product => product.IsCustomized == query.OnlyCustomized.Value);

        var total = await builder.CountAsync(ct);
        builder = query.Sort switch
        {
            ProductSearchSort.NameAsc => builder.OrderBy(product => product.Name),
            ProductSearchSort.PriceAsc => builder.OrderBy(product => product.Price),
            ProductSearchSort.PriceDesc => builder.OrderByDescending(product => product.Price),
            _ => builder.OrderByDescending(product => product.CreatedAt)
        };

        var items = await builder
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .SelectToListAsync(product => new ProductDto
            {
                Id = product.Id,
                Name = product.Name,
                Price = product.Price,
                Description = product.Description,
                SourceStoreId = product.SourceStoreId,
                IsCustomized = product.IsCustomized
            }, ct);

        return new PagedResult<ProductDto>(total, items, pageIndex, pageSize);
    }
}
