namespace Atlas.Infrastructure.Http.Configuration;

public sealed class ExternalHttpOptions
{
    public const string SectionName = "Atlas:Http";

    public ExternalHttpClientOptions Defaults { get; set; } = new();

    public Dictionary<string, ExternalHttpClientOptions> Clients { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);
}

public sealed class ExternalHttpClientOptions
{
    public string? BaseUrl { get; set; }

    public int? TimeoutSeconds { get; set; }

    public Dictionary<string, string> DefaultHeaders { get; set; } =
        new(StringComparer.OrdinalIgnoreCase);

    public ExternalHttpAuthenticationOptions Authentication { get; set; } = new();

    public ExternalHttpContextPropagationOptions ContextPropagation { get; set; } = new();

    public ExternalHttpLoggingOptions Logging { get; set; } = new();

    public ExternalHttpResilienceOptions Resilience { get; set; } = new();
}

public sealed class ExternalHttpAuthenticationOptions
{
    public string? ApiKey { get; set; }

    public string? ApiKeyHeaderName { get; set; }

    public string? BearerToken { get; set; }

    public string? AuthorizationScheme { get; set; }
}

public sealed class ExternalHttpContextPropagationOptions
{
    public bool? Enabled { get; set; }

    public string? CorrelationHeaderName { get; set; }

    public string? TraceIdHeaderName { get; set; }
}

public sealed class ExternalHttpLoggingOptions
{
    public bool? Enabled { get; set; }

    public bool? LogHeaders { get; set; }

    public bool? LogRequestBody { get; set; }

    public bool? LogResponseBody { get; set; }

    public int? MaxBodyChars { get; set; }

    public List<string> SensitiveHeaders { get; set; } = new();
}

public sealed class ExternalHttpResilienceOptions
{
    public ExternalHttpRetryOptions Retry { get; set; } = new();

    public ExternalHttpCircuitBreakerOptions CircuitBreaker { get; set; } = new();

    public ExternalHttpRateLimitOptions RateLimit { get; set; } = new();
}

public sealed class ExternalHttpRetryOptions
{
    public bool? Enabled { get; set; }

    public int? MaxAttempts { get; set; }

    public int? BaseDelayMilliseconds { get; set; }

    public int? MaxDelayMilliseconds { get; set; }

    public bool? UseJitter { get; set; }

    public List<int> StatusCodes { get; set; } = new();
}

public sealed class ExternalHttpCircuitBreakerOptions
{
    public bool? Enabled { get; set; }

    public int? FailureThreshold { get; set; }

    public int? BreakSeconds { get; set; }
}

public sealed class ExternalHttpRateLimitOptions
{
    public bool? Enabled { get; set; }

    public int? PermitLimit { get; set; }

    public int? WindowSeconds { get; set; }
}
