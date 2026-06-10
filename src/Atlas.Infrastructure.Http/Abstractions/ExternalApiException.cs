using System.Net;

namespace Atlas.Infrastructure.Http.Abstractions;

public class ExternalApiException : Exception
{
    public ExternalApiException(
        string providerName,
        string message,
        HttpStatusCode? statusCode = null,
        string? errorCode = null,
        string? responseBody = null,
        Uri? requestUri = null,
        string? method = null,
        bool isTransient = false,
        Exception? innerException = null)
        : base(message, innerException)
    {
        ProviderName = providerName;
        StatusCode = statusCode;
        ErrorCode = errorCode;
        ResponseBody = responseBody;
        RequestUri = requestUri;
        Method = method;
        IsTransient = isTransient;
    }

    public string ProviderName { get; }

    public HttpStatusCode? StatusCode { get; }

    public string? ErrorCode { get; }

    public string? ResponseBody { get; }

    public Uri? RequestUri { get; }

    public string? Method { get; }

    public bool IsTransient { get; }

    public static ExternalApiException CircuitOpen(string providerName, TimeSpan retryAfter)
    {
        return new ExternalApiException(
            providerName,
            $"External API circuit is open for provider '{providerName}'. Retry after {retryAfter.TotalSeconds:N0} seconds.",
            errorCode: "circuit_open",
            isTransient: true);
    }

    public static ExternalApiException RateLimited(string providerName, TimeSpan retryAfter)
    {
        return new ExternalApiException(
            providerName,
            $"External API local rate limit exceeded for provider '{providerName}'. Retry after {retryAfter.TotalSeconds:N0} seconds.",
            statusCode: HttpStatusCode.TooManyRequests,
            errorCode: "local_rate_limited",
            isTransient: true);
    }
}
