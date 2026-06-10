using Atlas.Infrastructure.Http.Configuration;
using Atlas.Infrastructure.Http.Internal;
using Microsoft.Extensions.Logging;

namespace Atlas.Infrastructure.Http.Handlers;

public sealed class ExternalHttpLoggingHandler : DelegatingHandler
{
    private readonly string _clientName;
    private readonly IExternalHttpClientOptionsResolver _optionsResolver;
    private readonly ILogger<ExternalHttpLoggingHandler> _logger;

    public ExternalHttpLoggingHandler(
        string clientName,
        IExternalHttpClientOptionsResolver optionsResolver,
        ILogger<ExternalHttpLoggingHandler> logger)
    {
        _clientName = clientName;
        _optionsResolver = optionsResolver;
        _logger = logger;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _optionsResolver.Get(_clientName).Logging;
        if (!options.Enabled)
            return await base.SendAsync(request, cancellationToken);

        var startedAt = DateTimeOffset.UtcNow;
        var safeUri = ExternalHttpRedactor.RedactUri(request.RequestUri);

        if (options.LogRequestBody && request.Content is not null)
        {
            var body = await request.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogDebug(
                "External HTTP request body for {Provider} {Method} {Uri}: {Body}",
                _clientName,
                request.Method.Method,
                safeUri,
                Truncate(ExternalHttpRedactor.RedactText(body), options.MaxBodyChars));
        }

        if (options.LogHeaders)
        {
            var headers = request.Headers.Select(static x => x);
            _logger.LogDebug(
                "External HTTP request headers for {Provider} {Method} {Uri}: {@Headers}",
                _clientName,
                request.Method.Method,
                safeUri,
                ExternalHttpRedactor.RedactHeaders(headers, options));
        }

        try
        {
            var response = await base.SendAsync(request, cancellationToken);
            var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;

            _logger.LogInformation(
                "External HTTP {Provider} {Method} {Uri} completed with {StatusCode} in {ElapsedMs} ms",
                _clientName,
                request.Method.Method,
                safeUri,
                (int)response.StatusCode,
                elapsedMs);

            if (options.LogResponseBody && response.Content is not null)
            {
                var body = await response.Content.ReadAsStringAsync(cancellationToken);
                _logger.LogDebug(
                    "External HTTP response body for {Provider} {Method} {Uri}: {Body}",
                    _clientName,
                    request.Method.Method,
                    safeUri,
                    Truncate(ExternalHttpRedactor.RedactText(body), options.MaxBodyChars));
            }

            return response;
        }
        catch (Exception ex) when (ex is not OperationCanceledException || !cancellationToken.IsCancellationRequested)
        {
            var elapsedMs = (DateTimeOffset.UtcNow - startedAt).TotalMilliseconds;
            _logger.LogWarning(
                ex,
                "External HTTP {Provider} {Method} {Uri} failed in {ElapsedMs} ms",
                _clientName,
                request.Method.Method,
                safeUri,
                elapsedMs);

            throw;
        }
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : value[..maxLength];
    }
}
