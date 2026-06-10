using System.Net;

namespace Atlas.Infrastructure.Http.Configuration;

public sealed class ResolvedExternalHttpClientOptions
{
    internal ResolvedExternalHttpClientOptions(
        string clientName,
        string? baseUrl,
        TimeSpan timeout,
        IReadOnlyDictionary<string, string> defaultHeaders,
        ResolvedExternalHttpAuthenticationOptions authentication,
        ResolvedExternalHttpContextPropagationOptions contextPropagation,
        ResolvedExternalHttpLoggingOptions logging,
        ResolvedExternalHttpResilienceOptions resilience)
    {
        ClientName = clientName;
        BaseUrl = baseUrl;
        Timeout = timeout;
        DefaultHeaders = defaultHeaders;
        Authentication = authentication;
        ContextPropagation = contextPropagation;
        Logging = logging;
        Resilience = resilience;
    }

    public string ClientName { get; }

    public string? BaseUrl { get; }

    public TimeSpan Timeout { get; }

    public IReadOnlyDictionary<string, string> DefaultHeaders { get; }

    public ResolvedExternalHttpAuthenticationOptions Authentication { get; }

    public ResolvedExternalHttpContextPropagationOptions ContextPropagation { get; }

    public ResolvedExternalHttpLoggingOptions Logging { get; }

    public ResolvedExternalHttpResilienceOptions Resilience { get; }
}

public sealed record ResolvedExternalHttpAuthenticationOptions(
    string? ApiKey,
    string? ApiKeyHeaderName,
    string? BearerToken,
    string AuthorizationScheme);

public sealed record ResolvedExternalHttpContextPropagationOptions(
    bool Enabled,
    string CorrelationHeaderName,
    string TraceIdHeaderName);

public sealed record ResolvedExternalHttpLoggingOptions(
    bool Enabled,
    bool LogHeaders,
    bool LogRequestBody,
    bool LogResponseBody,
    int MaxBodyChars,
    IReadOnlySet<string> SensitiveHeaders);

public sealed record ResolvedExternalHttpResilienceOptions(
    ResolvedExternalHttpRetryOptions Retry,
    ResolvedExternalHttpCircuitBreakerOptions CircuitBreaker,
    ResolvedExternalHttpRateLimitOptions RateLimit);

public sealed record ResolvedExternalHttpRetryOptions(
    bool Enabled,
    int MaxAttempts,
    TimeSpan BaseDelay,
    TimeSpan MaxDelay,
    bool UseJitter,
    IReadOnlySet<HttpStatusCode> StatusCodes);

public sealed record ResolvedExternalHttpCircuitBreakerOptions(
    bool Enabled,
    int FailureThreshold,
    TimeSpan BreakDuration);

public sealed record ResolvedExternalHttpRateLimitOptions(
    bool Enabled,
    int PermitLimit,
    TimeSpan Window);
