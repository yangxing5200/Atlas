using System.Net;
using Atlas.Infrastructure.Http.Abstractions;
using Atlas.Sample.WebApi.Integrations.SampleThirdParty;

namespace Atlas.Sample.WebApi.Services.ExternalHttpDemo;

public sealed class ProductSourcingQueryService : IProductSourcingQueryService
{
    private static readonly IReadOnlyDictionary<string, LocalProductProfile> LocalCatalog =
        new Dictionary<string, LocalProductProfile>(StringComparer.OrdinalIgnoreCase)
        {
            ["SKU-1001"] = new("SKU-1001", "Atlas smart shelf tag", "Retail Devices", "direct-store", 1.35m, 10),
            ["SKU-RETRY"] = new("SKU-RETRY", "Atlas retry demo item", "Reliability Demo", "direct-store", 1.25m, 5),
            ["SKU-LOWSTOCK"] = new("SKU-LOWSTOCK", "Atlas low-stock demo item", "Inventory Demo", "direct-store", 1.20m, 20),
            ["SKU-MISSING-SUPPLIER"] = new("SKU-MISSING-SUPPLIER", "Atlas supplier-missing demo item", "Supplier Demo", "direct-store", 1.30m, 1)
        };

    private readonly ISampleThirdPartyClient _supplierClient;

    public ProductSourcingQueryService(ISampleThirdPartyClient supplierClient)
    {
        _supplierClient = supplierClient;
    }

    public async Task<ProductSourcingQueryResult> GetSourcingSummaryAsync(
        string sku,
        bool unstableSupplier = false,
        CancellationToken cancellationToken = default)
    {
        if (!LocalCatalog.TryGetValue(sku, out var localProduct))
        {
            return ProductSourcingQueryResult.Failure(
                ProductSourcingQueryStatus.LocalProductNotFound,
                new ProductSourcingErrorResponse(
                    "local_product_not_found",
                    $"Atlas local catalog does not contain product '{sku}'."));
        }

        try
        {
            var supplierProduct = await _supplierClient.GetSupplierProductAsync(
                sku,
                unstableSupplier,
                cancellationToken);

            var suggestedRetailPrice = decimal.Round(
                supplierProduct.UnitPrice * localProduct.RetailMarkup,
                2,
                MidpointRounding.AwayFromZero);
            var canSell = supplierProduct.AvailableQuantity >= localProduct.MinimumSellableQuantity;

            return ProductSourcingQueryResult.Success(new ProductSourcingSummaryResponse(
                localProduct.Sku,
                localProduct.ProductName,
                localProduct.Category,
                localProduct.SalesChannel,
                new SupplierQuoteResponse(
                    _supplierClient.ProviderName,
                    supplierProduct.SupplierSku,
                    supplierProduct.ProductName,
                    supplierProduct.UnitPrice,
                    supplierProduct.Currency,
                    supplierProduct.AvailableQuantity,
                    supplierProduct.LeadTimeDays,
                    supplierProduct.UpdatedAt),
                suggestedRetailPrice,
                canSell,
                BuildFulfillmentMessage(canSell, supplierProduct)));
        }
        catch (ExternalApiException ex) when (ex.StatusCode == HttpStatusCode.NotFound)
        {
            return ProductSourcingQueryResult.Failure(
                ProductSourcingQueryStatus.SupplierProductNotFound,
                new ProductSourcingErrorResponse(
                    "supplier_product_not_found",
                    $"Supplier catalog does not contain product '{sku}'.",
                    ex.ProviderName,
                    ex.ErrorCode,
                    ex.StatusCode is null ? null : (int)ex.StatusCode.Value));
        }
        catch (ExternalApiException ex)
        {
            return ProductSourcingQueryResult.Failure(
                ProductSourcingQueryStatus.SupplierUnavailable,
                new ProductSourcingErrorResponse(
                    "supplier_unavailable",
                    "Supplier catalog is temporarily unavailable. Please retry later.",
                    ex.ProviderName,
                    ex.ErrorCode,
                    ex.StatusCode is null ? null : (int)ex.StatusCode.Value));
        }
    }

    private static string BuildFulfillmentMessage(
        bool canSell,
        SampleThirdPartyProductDto supplierProduct)
    {
        if (!canSell)
            return "Supplier stock is below the minimum sellable threshold.";

        return supplierProduct.LeadTimeDays <= 1
            ? "Supplier can fulfill this item within one day."
            : $"Supplier lead time is {supplierProduct.LeadTimeDays} days.";
    }

    private sealed record LocalProductProfile(
        string Sku,
        string ProductName,
        string Category,
        string SalesChannel,
        decimal RetailMarkup,
        int MinimumSellableQuantity);
}
