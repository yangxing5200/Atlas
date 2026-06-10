using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;

namespace Atlas.Sample.WebApi.Integrations.PaymentX;

public sealed class PaymentXSignatureHandler : DelegatingHandler
{
    public const string MerchantIdHeader = "X-Merchant-Id";
    public const string AccessKeyHeader = "X-Access-Key";
    public const string TimestampHeader = "X-Timestamp";
    public const string NonceHeader = "X-Nonce";
    public const string SignatureHeader = "X-Signature";

    private readonly IOptionsMonitor<PaymentXOptions> _options;

    public PaymentXSignatureHandler(IOptionsMonitor<PaymentXOptions> options)
    {
        _options = options;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _options.CurrentValue;
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var nonce = Guid.NewGuid().ToString("N");
        var body = request.Content is null
            ? string.Empty
            : await request.Content.ReadAsStringAsync(cancellationToken);

        var payload = BuildPayload(
            request.Method.Method,
            request.RequestUri?.PathAndQuery ?? "/",
            timestamp,
            nonce,
            body);
        var signature = Sign(payload, options.SecretKey);

        RemoveSignatureHeaders(request);
        request.Headers.TryAddWithoutValidation(MerchantIdHeader, options.MerchantId);
        request.Headers.TryAddWithoutValidation(AccessKeyHeader, options.AccessKey);
        request.Headers.TryAddWithoutValidation(TimestampHeader, timestamp);
        request.Headers.TryAddWithoutValidation(NonceHeader, nonce);
        request.Headers.TryAddWithoutValidation(SignatureHeader, signature);

        return await base.SendAsync(request, cancellationToken);
    }

    public static string BuildPayload(
        string method,
        string pathAndQuery,
        string timestamp,
        string nonce,
        string body)
    {
        return $"{method}\n{pathAndQuery}\n{timestamp}\n{nonce}\n{body}";
    }

    public static string Sign(string payload, string secret)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(secret));
        var hash = hmac.ComputeHash(Encoding.UTF8.GetBytes(payload));
        return Convert.ToHexString(hash).ToLowerInvariant();
    }

    private static void RemoveSignatureHeaders(HttpRequestMessage request)
    {
        request.Headers.Remove(MerchantIdHeader);
        request.Headers.Remove(AccessKeyHeader);
        request.Headers.Remove(TimestampHeader);
        request.Headers.Remove(NonceHeader);
        request.Headers.Remove(SignatureHeader);
    }
}
