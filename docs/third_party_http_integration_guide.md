# Third-Party HTTP Integration Guide

Atlas provides `Atlas.Infrastructure.Http` as the common foundation for outbound HTTP calls. The framework owns cross-cutting behavior such as configuration, timeouts, retries, circuit breaking, local rate limiting, authentication headers, log redaction, and unified error mapping.

Business code should not call arbitrary URLs through a shared `HttpHelper`. Add a typed client for each provider and expose provider-specific business methods.

## Configuration

HTTP clients are configured under `Atlas:Http`.

```json
{
  "Atlas": {
    "Http": {
      "Defaults": {
        "TimeoutSeconds": 10,
        "Resilience": {
          "Retry": {
            "Enabled": true,
            "MaxAttempts": 3,
            "BaseDelayMilliseconds": 100,
            "MaxDelayMilliseconds": 1000,
            "StatusCodes": [408, 429, 500, 502, 503, 504]
          }
        }
      },
      "Clients": {
        "PaymentX": {
          "BaseUrl": "https://api.paymentx.example",
          "TimeoutSeconds": 5,
          "Authentication": {
            "ApiKeyHeaderName": "X-Api-Key",
            "ApiKey": "<from secret store>"
          }
        }
      }
    }
  }
}
```

## Registration

Register each provider as a typed client:

```csharp
builder.Services.AddAtlasExternalHttpClient<IPaymentXClient, PaymentXClient>(
    builder.Configuration,
    "PaymentX");
```

`AddAtlasCore()` registers the shared HTTP foundation through `AddAtlasHttp()`. Provider registrations still need `AddAtlasExternalHttpClient` so handlers can be attached with the correct provider name.

## Client Shape

Expose business operations, not raw HTTP operations.

```csharp
public interface IPaymentXClient : IExternalApiClient
{
    Task<CreatePaymentResponse> CreatePaymentAsync(
        CreatePaymentRequest request,
        CancellationToken ct = default);
}
```

Inside the implementation, use `IExternalApiExecutor` so status codes and provider error bodies are mapped consistently.

For write operations such as payment, refund, shipping, coupon issuance, and order creation, do not rely on automatic retry unless the provider supports idempotency. Add an `Idempotency-Key` header or set `ExternalApiRequestOptions.IsIdempotent = true` only when the request is safe to repeat.

## Reliability Defaults

- Retry is enabled by default for `408`, `429`, and common `5xx` responses.
- Automatic retry only happens for safe methods, explicit idempotent requests, or requests with `Idempotency-Key`.
- Circuit breaker and rate limiting are available per provider and are disabled by default unless configured.
- `HttpClientFactory` is used for all clients; do not manually `new HttpClient()`.

## Security

- Never hard-code real API keys in repository files.
- Keep provider credentials in environment variables, user secrets, or a secret store.
- Header and body logging are redacted for common secret names such as `Authorization`, `token`, `secret`, `apiKey`, and `password`.
- Request and response bodies are not logged by default.

## Observability

Atlas already registers OpenTelemetry `HttpClient` instrumentation in `AddAtlasOpenTelemetry()`. When observability is enabled, typed clients created by `AddAtlasExternalHttpClient` produce outbound HTTP spans.

## Sample

`samples/Atlas.Sample.WebApi` contains a local demo client named `SampleThirdParty`.

Try:

```http
GET http://localhost:5212/api/external-http-demo/products/SKU-1001/sourcing-summary
GET http://localhost:5212/api/external-http-demo/products/SKU-RETRY/sourcing-summary?unstableSupplier=true
GET http://localhost:5212/api/external-http-demo/products/SKU-MISSING-SUPPLIER/sourcing-summary
GET http://localhost:5212/api/external-http-demo/products/SKU-LOWSTOCK/sourcing-summary
```

The business endpoint calls `ProductSourcingQueryService`, which combines a local product profile with a supplier quote from `SampleThirdPartyClient`. The `unstableSupplier=true` example returns `503` once from the mock upstream, then succeeds through the idempotent GET retry path. The response is an Atlas business response, not the supplier DTO.

## Custom Auth Sample

`samples/Atlas.Sample.WebApi` also contains a fuller payment provider demo named `PaymentX`.

It demonstrates:

- `appsettings.Development.json` using a sandbox host: `http://localhost:5212`.
- `appsettings.Production.json` using a production host: `https://api.paymentx.example`.
- Provider-specific `PaymentXOptions.ApiPrefix` so sandbox and production can use different path prefixes.
- `PaymentXSignatureHandler` adding HMAC headers for each outbound request.
- `OrderPaymentDemoService` calling `IPaymentXClient` and returning an Atlas business response.
- A mock upstream controller validating the HMAC signature and `Idempotency-Key`.

Try:

```http
POST http://localhost:5212/api/payment-demo/orders/90001/pay
POST http://localhost:5212/api/payment-demo/orders/90002/pay
POST http://localhost:5212/api/payment-demo/orders/99999/pay
```

The second request can set `simulateTransientFailure` to `true`. The mock upstream returns `503` once, and the Atlas HTTP pipeline retries because `PaymentXClient` sends an `Idempotency-Key` and marks the request as idempotent.

Production credentials should be supplied by environment variables or a secret store, for example:

```text
Integrations__PaymentX__MerchantId=...
Integrations__PaymentX__AccessKey=...
Integrations__PaymentX__SecretKey=...
```
