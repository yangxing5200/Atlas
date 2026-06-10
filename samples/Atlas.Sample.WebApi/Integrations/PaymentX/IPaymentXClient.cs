using Atlas.Infrastructure.Http.Abstractions;

namespace Atlas.Sample.WebApi.Integrations.PaymentX;

public interface IPaymentXClient : IExternalApiClient
{
    Task<PaymentXCreatePaymentResponse> CreatePaymentAsync(
        PaymentXCreatePaymentRequest request,
        string idempotencyKey,
        bool unstable = false,
        CancellationToken cancellationToken = default);
}
