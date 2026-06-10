namespace Atlas.Sample.WebApi.Services.ExternalHttpDemo;

public sealed record ProductSourcingQueryResult(
    ProductSourcingQueryStatus Status,
    ProductSourcingSummaryResponse? Summary,
    ProductSourcingErrorResponse? Error)
{
    public static ProductSourcingQueryResult Success(ProductSourcingSummaryResponse summary)
    {
        return new ProductSourcingQueryResult(ProductSourcingQueryStatus.Success, summary, null);
    }

    public static ProductSourcingQueryResult Failure(
        ProductSourcingQueryStatus status,
        ProductSourcingErrorResponse error)
    {
        return new ProductSourcingQueryResult(status, null, error);
    }
}

public enum ProductSourcingQueryStatus
{
    Success,
    LocalProductNotFound,
    SupplierProductNotFound,
    SupplierUnavailable
}

public sealed record ProductSourcingSummaryResponse(
    string Sku,
    string ProductName,
    string Category,
    string SalesChannel,
    SupplierQuoteResponse Supplier,
    decimal SuggestedRetailPrice,
    bool CanSell,
    string FulfillmentMessage);

public sealed record SupplierQuoteResponse(
    string Provider,
    string SupplierSku,
    string SupplierProductName,
    decimal UnitPrice,
    string Currency,
    int AvailableQuantity,
    int LeadTimeDays,
    DateTimeOffset UpdatedAt);

public sealed record ProductSourcingErrorResponse(
    string Code,
    string Message,
    string? Provider = null,
    string? UpstreamErrorCode = null,
    int? UpstreamStatusCode = null);
