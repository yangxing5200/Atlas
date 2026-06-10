namespace Atlas.Sample.WebApi.Services.PaymentDemo;

public interface IOrderPaymentDemoService
{
    Task<OrderPaymentDemoResult> PayAsync(
        long orderId,
        PayOrderDemoRequest request,
        CancellationToken cancellationToken = default);
}
