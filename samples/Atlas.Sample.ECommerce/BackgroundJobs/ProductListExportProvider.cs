using Atlas.Exporting;
using Atlas.Models.DTOs;
using Atlas.Models.Tenant.Requests;
using Atlas.Sample.ECommerce.Models;
using Atlas.Services.Abstractions.Queries;

namespace Atlas.Sample.ECommerce.BackgroundJobs;

public sealed class ProductListExportProvider : ExportTaskProvider<ExportProductsRequest>
{
    private readonly IProductQueryService _products;

    public ProductListExportProvider(IProductQueryService products)
    {
        _products = products ?? throw new ArgumentNullException(nameof(products));
    }

    public override string ExportTaskType => SampleECommerceExportTaskTypes.ProductList;

    public override string ResourceCode => "product";

    public override string PermissionCode => SampleECommercePermissionCodes.ProductsExport;

    public override IReadOnlyList<ExportColumn> Columns { get; } =
    [
        new("id", "Id") { ValueKind = ExportValueKind.Number },
        new("name", "Name"),
        new("price", "Price") { ValueKind = ExportValueKind.Number, Format = "0.00" },
        new("description", "Description"),
        new("isCustomized", "Is customized") { ValueKind = ExportValueKind.Boolean }
    ];

    public override async Task<ExportPage> ReadPageAsync(
        ExportTaskContext<ExportProductsRequest> context,
        int pageIndex,
        int pageSize,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var criteria = ExportSearchRequest.GetCriteria(context.Query);

        var result = await _products.SearchAsync(
            new ProductSearchQuery
            {
                Keyword = criteria.Keyword,
                StoreId = criteria.StoreId,
                MinPrice = criteria.MinPrice,
                MaxPrice = criteria.MaxPrice,
                OnlyCustomized = criteria.OnlyCustomized,
                Sort = criteria.Sort,
                PageIndex = pageIndex,
                PageSize = pageSize
            },
            ct);

        var rows = result.Items
            .Select<ProductDto, IReadOnlyDictionary<string, ExportCellValue>>(product => new Dictionary<string, ExportCellValue>
            {
                ["id"] = product.Id,
                ["name"] = product.Name,
                ["price"] = product.Price,
                ["description"] = product.Description,
                ["isCustomized"] = product.IsCustomized
            })
            .ToArray();

        return new ExportPage(rows, result.Total);
    }
}
