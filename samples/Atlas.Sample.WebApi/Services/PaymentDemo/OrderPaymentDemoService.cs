using Atlas.Infrastructure.Http.Abstractions;
using Atlas.Sample.WebApi.Integrations.PaymentX;

namespace Atlas.Sample.WebApi.Services.PaymentDemo;

public sealed class OrderPaymentDemoService : IOrderPaymentDemoService
{
    private static readonly IReadOnlyDictionary<long, LocalOrderSnapshot> Orders =
        new Dictionary<long, LocalOrderSnapshot>
        {
            [90001] = new(90001, "ORD-90001", "Atlas smart shelf starter kit", 199.00m, "CNY"),
            [90002] = new(90002, "ORD-90002", "Atlas retry payment demo order", 299.00m, "CNY")
        };

    private readonly IPaymentXClient _paymentX;

    public OrderPaymentDemoService(IPaymentXClient paymentX)
    {
        _paymentX = paymentX;
    }

    public async Task<OrderPaymentDemoResult> PayAsync(
        long orderId,
        PayOrderDemoRequest request,
        CancellationToken cancellationToken = default)
    {
        if (!Orders.TryGetValue(orderId, out var order))
        {
            return OrderPaymentDemoResult.Failure(
                OrderPaymentDemoStatus.LocalOrderNotFound,
                new OrderPaymentDemoErrorResponse(
                    "local_order_not_found",
                    $"Atlas local demo order '{orderId}' does not exist."));
        }

        var idempotencyKey = $"payment-demo:{order.OrderNo}";

        try
        {
            var payment = await _paymentX.CreatePaymentAsync(
                new PaymentXCreatePaymentRequest
                {
                    MerchantOrderNo = order.OrderNo,
                    Amount = order.Amount,
                    Currency = order.Currency,
                    Subject = order.Subject,
                    NotifyUrl = "https://atlas.example/callbacks/paymentx"
                },
                idempotencyKey,
                request.SimulateTransientFailure,
                cancellationToken);

            return OrderPaymentDemoResult.Success(new OrderPaymentDemoResponse(
                order.OrderId,
                order.OrderNo,
                _paymentX.ProviderName,
                payment.PaymentId,
                payment.Status,
                payment.Amount,
                payment.Currency,
                idempotencyKey,
                payment.CreatedAt));
        }
        catch (ExternalApiException ex)
        {
            return OrderPaymentDemoResult.Failure(
                OrderPaymentDemoStatus.PaymentProviderUnavailable,
                new OrderPaymentDemoErrorResponse(
                    "payment_provider_unavailable",
                    "Payment provider is temporarily unavailable or rejected the request.",
                    ex.ProviderName,
                    ex.ErrorCode,
                    ex.StatusCode is null ? null : (int)ex.StatusCode.Value));
        }
    }

    private sealed record LocalOrderSnapshot(
        long OrderId,
        string OrderNo,
        string Subject,
        decimal Amount,
        string Currency);
}
