using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Http.Configuration;

public sealed class ExternalHttpOptionsValidator : IValidateOptions<ExternalHttpOptions>
{
    public ValidateOptionsResult Validate(string? name, ExternalHttpOptions options)
    {
        var failures = new List<string>();

        ValidateClient("Defaults", options.Defaults, failures);

        foreach (var client in options.Clients)
        {
            if (string.IsNullOrWhiteSpace(client.Key))
            {
                failures.Add("Atlas:Http:Clients contains an empty client name.");
                continue;
            }

            ValidateClient($"Clients:{client.Key}", client.Value, failures);
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }

    private static void ValidateClient(
        string path,
        ExternalHttpClientOptions options,
        ICollection<string> failures)
    {
        if (!string.IsNullOrWhiteSpace(options.BaseUrl) &&
            !Uri.TryCreate(options.BaseUrl, UriKind.Absolute, out _))
        {
            failures.Add($"Atlas:Http:{path}:BaseUrl must be an absolute URI.");
        }

        ValidatePositive(options.TimeoutSeconds, $"Atlas:Http:{path}:TimeoutSeconds", failures);
        ValidatePositive(options.Logging.MaxBodyChars, $"Atlas:Http:{path}:Logging:MaxBodyChars", failures);
        ValidatePositive(options.Resilience.Retry.MaxAttempts, $"Atlas:Http:{path}:Resilience:Retry:MaxAttempts", failures);
        ValidatePositive(options.Resilience.Retry.BaseDelayMilliseconds, $"Atlas:Http:{path}:Resilience:Retry:BaseDelayMilliseconds", failures);
        ValidatePositive(options.Resilience.Retry.MaxDelayMilliseconds, $"Atlas:Http:{path}:Resilience:Retry:MaxDelayMilliseconds", failures);
        ValidatePositive(options.Resilience.CircuitBreaker.FailureThreshold, $"Atlas:Http:{path}:Resilience:CircuitBreaker:FailureThreshold", failures);
        ValidatePositive(options.Resilience.CircuitBreaker.BreakSeconds, $"Atlas:Http:{path}:Resilience:CircuitBreaker:BreakSeconds", failures);
        ValidatePositive(options.Resilience.RateLimit.PermitLimit, $"Atlas:Http:{path}:Resilience:RateLimit:PermitLimit", failures);
        ValidatePositive(options.Resilience.RateLimit.WindowSeconds, $"Atlas:Http:{path}:Resilience:RateLimit:WindowSeconds", failures);

        foreach (var statusCode in options.Resilience.Retry.StatusCodes)
        {
            if (statusCode < 100 || statusCode > 599)
                failures.Add($"Atlas:Http:{path}:Resilience:Retry:StatusCodes contains invalid HTTP status code {statusCode}.");
        }
    }

    private static void ValidatePositive(int? value, string path, ICollection<string> failures)
    {
        if (value.HasValue && value <= 0)
            failures.Add($"{path} must be greater than zero.");
    }
}
