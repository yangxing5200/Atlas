namespace Atlas.Models.Tenant.Requests;

public sealed class ProductSearchQuery
{
    public string? Keyword { get; init; }

    public long? StoreId { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public bool? OnlyCustomized { get; init; }

    public ProductSearchSort Sort { get; init; } = ProductSearchSort.CreatedAtDesc;

    public int PageIndex { get; init; } = 1;

    public int PageSize { get; init; } = 20;
}

public enum ProductSearchSort
{
    CreatedAtDesc = 0,
    NameAsc = 1,
    PriceAsc = 2,
    PriceDesc = 3
}
