using System.Net;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Http.Configuration;

public sealed class ExternalHttpClientOptionsResolver : IExternalHttpClientOptionsResolver
{
    private static readonly HttpStatusCode[] DefaultRetryStatusCodes =
    [
        HttpStatusCode.RequestTimeout,
        HttpStatusCode.TooManyRequests,
        HttpStatusCode.InternalServerError,
        HttpStatusCode.BadGateway,
        HttpStatusCode.ServiceUnavailable,
        HttpStatusCode.GatewayTimeout
    ];

    private static readonly string[] DefaultSensitiveHeaders =
    [
        "Authorization",
        "Proxy-Authorization",
        "X-Api-Key",
        "ApiKey",
        "api-key",
        "token",
        "secret"
    ];

    private readonly IOptionsMonitor<ExternalHttpOptions> _options;

    public ExternalHttpClientOptionsResolver(IOptionsMonitor<ExternalHttpOptions> options)
    {
        _options = options;
    }

    public ResolvedExternalHttpClientOptions Get(string clientName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        var root = _options.CurrentValue;
        var defaults = root.Defaults ?? new ExternalHttpClientOptions();
        root.Clients ??= new Dictionary<string, ExternalHttpClientOptions>(StringComparer.OrdinalIgnoreCase);
        root.Clients.TryGetValue(clientName, out var client);
        client ??= new ExternalHttpClientOptions();

        var defaultHeaders = MergeDictionary(defaults.DefaultHeaders, client.DefaultHeaders);
        var sensitiveHeaders = MergeList(defaults.Logging.SensitiveHeaders, client.Logging.SensitiveHeaders, DefaultSensitiveHeaders);

        return new ResolvedExternalHttpClientOptions(
            clientName,
            FirstNotBlank(client.BaseUrl, defaults.BaseUrl),
            TimeSpan.FromSeconds(PositiveOrDefault(client.TimeoutSeconds, defaults.TimeoutSeconds, 30)),
            defaultHeaders,
            ResolveAuthentication(defaults.Authentication, client.Authentication),
            ResolveContextPropagation(defaults.ContextPropagation, client.ContextPropagation),
            ResolveLogging(defaults.Logging, client.Logging, sensitiveHeaders),
            ResolveResilience(defaults.Resilience, client.Resilience));
    }

    private static ResolvedExternalHttpAuthenticationOptions ResolveAuthentication(
        ExternalHttpAuthenticationOptions defaults,
        ExternalHttpAuthenticationOptions client)
    {
        return new ResolvedExternalHttpAuthenticationOptions(
            FirstNotBlank(client.ApiKey, defaults.ApiKey),
            FirstNotBlank(client.ApiKeyHeaderName, defaults.ApiKeyHeaderName),
            FirstNotBlank(client.BearerToken, defaults.BearerToken),
            FirstNotBlank(client.AuthorizationScheme, defaults.AuthorizationScheme) ?? "Bearer");
    }

    private static ResolvedExternalHttpContextPropagationOptions ResolveContextPropagation(
        ExternalHttpContextPropagationOptions defaults,
        ExternalHttpContextPropagationOptions client)
    {
        return new ResolvedExternalHttpContextPropagationOptions(
            client.Enabled ?? defaults.Enabled ?? true,
            FirstNotBlank(client.CorrelationHeaderName, defaults.CorrelationHeaderName) ?? "X-Correlation-Id",
            FirstNotBlank(client.TraceIdHeaderName, defaults.TraceIdHeaderName) ?? "X-Trace-Id");
    }

    private static ResolvedExternalHttpLoggingOptions ResolveLogging(
        ExternalHttpLoggingOptions defaults,
        ExternalHttpLoggingOptions client,
        IReadOnlySet<string> sensitiveHeaders)
    {
        return new ResolvedExternalHttpLoggingOptions(
            client.Enabled ?? defaults.Enabled ?? true,
            client.LogHeaders ?? defaults.LogHeaders ?? true,
            client.LogRequestBody ?? defaults.LogRequestBody ?? false,
            client.LogResponseBody ?? defaults.LogResponseBody ?? false,
            PositiveOrDefault(client.MaxBodyChars, defaults.MaxBodyChars, 2048),
            sensitiveHeaders);
    }

    private static ResolvedExternalHttpResilienceOptions ResolveResilience(
        ExternalHttpResilienceOptions defaults,
        ExternalHttpResilienceOptions client)
    {
        var retryStatusCodes = MergeStatusCodes(defaults.Retry.StatusCodes, client.Retry.StatusCodes);

        var retry = new ResolvedExternalHttpRetryOptions(
            client.Retry.Enabled ?? defaults.Retry.Enabled ?? true,
            Math.Max(1, PositiveOrDefault(client.Retry.MaxAttempts, defaults.Retry.MaxAttempts, 3)),
            TimeSpan.FromMilliseconds(PositiveOrDefault(client.Retry.BaseDelayMilliseconds, defaults.Retry.BaseDelayMilliseconds, 200)),
            TimeSpan.FromMilliseconds(PositiveOrDefault(client.Retry.MaxDelayMilliseconds, defaults.Retry.MaxDelayMilliseconds, 2000)),
            client.Retry.UseJitter ?? defaults.Retry.UseJitter ?? true,
            retryStatusCodes);

        var circuitBreaker = new ResolvedExternalHttpCircuitBreakerOptions(
            client.CircuitBreaker.Enabled ?? defaults.CircuitBreaker.Enabled ?? false,
            Math.Max(1, PositiveOrDefault(client.CircuitBreaker.FailureThreshold, defaults.CircuitBreaker.FailureThreshold, 5)),
            TimeSpan.FromSeconds(PositiveOrDefault(client.CircuitBreaker.BreakSeconds, defaults.CircuitBreaker.BreakSeconds, 30)));

        var rateLimit = new ResolvedExternalHttpRateLimitOptions(
            client.RateLimit.Enabled ?? defaults.RateLimit.Enabled ?? false,
            Math.Max(1, PositiveOrDefault(client.RateLimit.PermitLimit, defaults.RateLimit.PermitLimit, 100)),
            TimeSpan.FromSeconds(PositiveOrDefault(client.RateLimit.WindowSeconds, defaults.RateLimit.WindowSeconds, 1)));

        return new ResolvedExternalHttpResilienceOptions(retry, circuitBreaker, rateLimit);
    }

    private static IReadOnlyDictionary<string, string> MergeDictionary(
        IDictionary<string, string>? defaults,
        IDictionary<string, string>? client)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        if (defaults is not null)
        {
            foreach (var item in defaults)
                result[item.Key] = item.Value;
        }

        if (client is not null)
        {
            foreach (var item in client)
                result[item.Key] = item.Value;
        }

        return result;
    }

    private static IReadOnlySet<string> MergeList(
        IEnumerable<string>? defaults,
        IEnumerable<string>? client,
        IEnumerable<string> fallback)
    {
        var result = new HashSet<string>(fallback, StringComparer.OrdinalIgnoreCase);

        if (defaults is not null)
        {
            foreach (var item in defaults.Where(static x => !string.IsNullOrWhiteSpace(x)))
                result.Add(item);
        }

        if (client is not null)
        {
            foreach (var item in client.Where(static x => !string.IsNullOrWhiteSpace(x)))
                result.Add(item);
        }

        return result;
    }

    private static IReadOnlySet<HttpStatusCode> MergeStatusCodes(
        IEnumerable<int>? defaults,
        IEnumerable<int>? client)
    {
        var source = client is not null && client.Any()
            ? client
            : defaults is not null && defaults.Any()
                ? defaults
                : DefaultRetryStatusCodes.Select(static x => (int)x);

        return source
            .Where(static code => code >= 100 && code <= 599)
            .Select(static code => (HttpStatusCode)code)
            .ToHashSet();
    }

    private static int PositiveOrDefault(int? client, int? defaults, int fallback)
    {
        if (client is > 0)
            return client.Value;

        if (defaults is > 0)
            return defaults.Value;

        return fallback;
    }

    private static string? FirstNotBlank(string? preferred, string? fallback)
    {
        if (!string.IsNullOrWhiteSpace(preferred))
            return preferred.Trim();

        return string.IsNullOrWhiteSpace(fallback) ? null : fallback.Trim();
    }
}
