using Atlas.Exporting;
using Atlas.Models.Tenant.Requests;

namespace Atlas.Sample.ECommerce.Models;

public sealed class ExportProductsRequest : ExportListRequest<ProductExportCriteria>
{
}

public sealed class ProductExportCriteria
{
    public string? Keyword { get; init; }

    public long? StoreId { get; init; }

    public decimal? MinPrice { get; init; }

    public decimal? MaxPrice { get; init; }

    public bool? OnlyCustomized { get; init; }

    public ProductSearchSort Sort { get; init; } = ProductSearchSort.CreatedAtDesc;
}
