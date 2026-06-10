namespace Atlas.Sample.WebApi.Services.PaymentDemo;

public sealed class PayOrderDemoRequest
{
    public string? PaymentChannel { get; set; }

    public bool SimulateTransientFailure { get; set; }
}

public sealed record OrderPaymentDemoResult(
    OrderPaymentDemoStatus Status,
    OrderPaymentDemoResponse? Payment,
    OrderPaymentDemoErrorResponse? Error)
{
    public static OrderPaymentDemoResult Success(OrderPaymentDemoResponse payment)
    {
        return new OrderPaymentDemoResult(OrderPaymentDemoStatus.Success, payment, null);
    }

    public static OrderPaymentDemoResult Failure(
        OrderPaymentDemoStatus status,
        OrderPaymentDemoErrorResponse error)
    {
        return new OrderPaymentDemoResult(status, null, error);
    }
}

public enum OrderPaymentDemoStatus
{
    Success,
    LocalOrderNotFound,
    PaymentProviderUnavailable
}

public sealed record OrderPaymentDemoResponse(
    long OrderId,
    string OrderNo,
    string PaymentProvider,
    string PaymentId,
    string PaymentStatus,
    decimal Amount,
    string Currency,
    string IdempotencyKey,
    DateTimeOffset CreatedAt);

public sealed record OrderPaymentDemoErrorResponse(
    string Code,
    string Message,
    string? Provider = null,
    string? UpstreamErrorCode = null,
    int? UpstreamStatusCode = null);
