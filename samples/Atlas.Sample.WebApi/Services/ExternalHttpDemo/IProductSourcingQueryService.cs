namespace Atlas.Sample.WebApi.Services.ExternalHttpDemo;

public interface IProductSourcingQueryService
{
    Task<ProductSourcingQueryResult> GetSourcingSummaryAsync(
        string sku,
        bool unstableSupplier = false,
        CancellationToken cancellationToken = default);
}
