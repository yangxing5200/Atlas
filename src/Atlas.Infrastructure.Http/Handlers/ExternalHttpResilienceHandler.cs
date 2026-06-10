using System.Net;
using Atlas.Infrastructure.Http.Abstractions;
using Atlas.Infrastructure.Http.Configuration;
using Atlas.Infrastructure.Http.Internal;
using Atlas.Infrastructure.Http.Resilience;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Http.Handlers;

public sealed class ExternalHttpResilienceHandler : DelegatingHandler
{
    private static readonly HttpMethod[] SafeMethods =
    [
        HttpMethod.Get,
        HttpMethod.Head,
        HttpMethod.Options,
        HttpMethod.Trace,
        HttpMethod.Delete
    ];

    private readonly string _clientName;
    private readonly IExternalHttpClientOptionsResolver _optionsResolver;
    private readonly ExternalHttpResilienceStateRegistry _stateRegistry;
    private readonly ILogger<ExternalHttpResilienceHandler> _logger;

    public ExternalHttpResilienceHandler(
        string clientName,
        IExternalHttpClientOptionsResolver optionsResolver,
        ExternalHttpResilienceStateRegistry stateRegistry,
        ILogger<ExternalHttpResilienceHandler> logger)
    {
        _clientName = clientName;
        _optionsResolver = optionsResolver;
        _stateRegistry = stateRegistry;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _optionsResolver.Get(_clientName).Resilience;
        EnsureCircuitAllowsCall(options.CircuitBreaker);
        EnsureRateLimitAllowsCall(options.RateLimit);

        var retry = options.Retry;
        var canRetry = retry.Enabled && IsRetrySafe(request);
        var maxAttempts = canRetry ? retry.MaxAttempts : 1;
        var bufferedRequest = maxAttempts > 1
            ? await HttpRequestMessageCloner.BufferAsync(request, cancellationToken)
            : null;

        for (var attempt = 1; attempt <= maxAttempts; attempt++)
        {
            var attemptRequest = attempt == 1
                ? request
                : HttpRequestMessageCloner.Clone(request, bufferedRequest!);

            try
            {
                var response = await base.SendAsync(attemptRequest, cancellationToken);

                if (!ShouldRetry(response.StatusCode, retry) || attempt >= maxAttempts)
                {
                    RecordCircuitResult(options.CircuitBreaker, response);
                    return response;
                }

                RecordCircuitFailure(options.CircuitBreaker);
                _logger.LogWarning(
                    "External HTTP {Provider} received retryable status {StatusCode} on attempt {Attempt}/{MaxAttempts}.",
                    _clientName,
                    (int)response.StatusCode,
                    attempt,
                    maxAttempts);

                response.Dispose();
                await DelayBeforeRetryAsync(attempt, retry, cancellationToken);
            }
            catch (Exception ex) when (IsTransientException(ex, cancellationToken) && attempt < maxAttempts)
            {
                RecordCircuitFailure(options.CircuitBreaker);
                _logger.LogWarning(
                    ex,
                    "External HTTP {Provider} failed on attempt {Attempt}/{MaxAttempts}; retrying.",
                    _clientName,
                    attempt,
                    maxAttempts);

                await DelayBeforeRetryAsync(attempt, retry, cancellationToken);
            }
            catch
            {
                RecordCircuitFailure(options.CircuitBreaker);
                throw;
            }
        }

        throw new InvalidOperationException("Retry loop exited unexpectedly.");
    }

    private void EnsureCircuitAllowsCall(ResolvedExternalHttpCircuitBreakerOptions options)
    {
        if (!options.Enabled)
            return;

        var lease = _stateRegistry.CheckCircuit(_clientName);
        if (lease.IsOpen)
            throw ExternalApiException.CircuitOpen(_clientName, lease.RetryAfter);
    }

    private void EnsureRateLimitAllowsCall(ResolvedExternalHttpRateLimitOptions options)
    {
        if (!options.Enabled)
            return;

        var lease = _stateRegistry.TryAcquireRateLimit(_clientName, options.PermitLimit, options.Window);
        if (!lease.IsAcquired)
            throw ExternalApiException.RateLimited(_clientName, lease.RetryAfter);
    }

    private void RecordCircuitResult(
        ResolvedExternalHttpCircuitBreakerOptions options,
        HttpResponseMessage response)
    {
        if (!options.Enabled)
            return;

        if ((int)response.StatusCode < 500 && response.StatusCode != HttpStatusCode.RequestTimeout)
            _stateRegistry.RecordCircuitSuccess(_clientName);
        else
            RecordCircuitFailure(options);
    }

    private void RecordCircuitFailure(ResolvedExternalHttpCircuitBreakerOptions options)
    {
        if (!options.Enabled)
            return;

        _stateRegistry.RecordCircuitFailure(_clientName, options.FailureThreshold, options.BreakDuration);
    }

    private static bool ShouldRetry(
        HttpStatusCode statusCode,
        ResolvedExternalHttpRetryOptions options)
    {
        return options.StatusCodes.Contains(statusCode);
    }

    private static bool IsRetrySafe(HttpRequestMessage request)
    {
        if (request.Options.TryGetValue(ExternalHttpRequestOptions.IsIdempotent, out var isIdempotent))
            return isIdempotent;

        if (request.Headers.Contains("Idempotency-Key"))
            return true;

        return SafeMethods.Contains(request.Method);
    }

    private static bool IsTransientException(Exception exception, CancellationToken cancellationToken)
    {
        if (cancellationToken.IsCancellationRequested)
            return false;

        return exception is HttpRequestException or TaskCanceledException;
    }

    private static Task DelayBeforeRetryAsync(
        int attempt,
        ResolvedExternalHttpRetryOptions options,
        CancellationToken cancellationToken)
    {
        var exponentialMs = options.BaseDelay.TotalMilliseconds * Math.Pow(2, attempt - 1);
        var cappedMs = Math.Min(exponentialMs, options.MaxDelay.TotalMilliseconds);
        var jitterMs = options.UseJitter ? Random.Shared.Next(0, 100) : 0;
        return Task.Delay(TimeSpan.FromMilliseconds(cappedMs + jitterMs), cancellationToken);
    }
}
