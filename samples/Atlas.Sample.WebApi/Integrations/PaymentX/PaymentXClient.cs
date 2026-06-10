using System.Net.Http.Json;
using Atlas.Infrastructure.Http.Abstractions;
using Microsoft.Extensions.Options;

namespace Atlas.Sample.WebApi.Integrations.PaymentX;

public sealed class PaymentXClient : IPaymentXClient
{
    public const string ClientName = "PaymentX";

    private readonly HttpClient _httpClient;
    private readonly IExternalApiExecutor _executor;
    private readonly IOptionsMonitor<PaymentXOptions> _options;

    public PaymentXClient(
        HttpClient httpClient,
        IExternalApiExecutor executor,
        IOptionsMonitor<PaymentXOptions> options)
    {
        _httpClient = httpClient;
        _executor = executor;
        _options = options;
    }

    public string ProviderName => ClientName;

    public Task<PaymentXCreatePaymentResponse> CreatePaymentAsync(
        PaymentXCreatePaymentRequest request,
        string idempotencyKey,
        bool unstable = false,
        CancellationToken cancellationToken = default)
    {
        var options = _options.CurrentValue;
        var path = $"{NormalizePrefix(options.ApiPrefix)}/payments";
        if (unstable)
            path += "?unstable=true";

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, path)
        {
            Content = JsonContent.Create(request)
        };
        httpRequest.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);

        return _executor.SendAsync<PaymentXCreatePaymentResponse>(
            ProviderName,
            _httpClient,
            httpRequest,
            new ExternalApiRequestOptions
            {
                OperationName = "PaymentX.CreatePayment",
                IsIdempotent = true
            },
            cancellationToken);
    }

    private static string NormalizePrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
            return string.Empty;

        return prefix.StartsWith('/') ? prefix.TrimEnd('/') : $"/{prefix.TrimEnd('/')}";
    }
}
