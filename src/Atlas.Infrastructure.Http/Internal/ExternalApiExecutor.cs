using System.Net.Http.Json;
using System.Text.Json;
using Atlas.Infrastructure.Http.Abstractions;

namespace Atlas.Infrastructure.Http.Internal;

internal sealed class ExternalApiExecutor : IExternalApiExecutor
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    private readonly IExternalApiErrorParser _errorParser;

    public ExternalApiExecutor(IExternalApiErrorParser errorParser)
    {
        _errorParser = errorParser;
    }

    public async Task<TResponse> SendAsync<TResponse>(
        string providerName,
        HttpClient httpClient,
        HttpRequestMessage request,
        ExternalApiRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(providerName, httpClient, request, options, cancellationToken);

        if (typeof(TResponse) == typeof(string))
        {
            var text = await response.Content.ReadAsStringAsync(cancellationToken);
            return (TResponse)(object)text;
        }

        var result = await response.Content.ReadFromJsonAsync<TResponse>(JsonOptions, cancellationToken);
        if (result is null)
        {
            throw new ExternalApiException(
                providerName,
                $"External API '{providerName}' returned an empty response body.",
                statusCode: response.StatusCode,
                requestUri: request.RequestUri,
                method: request.Method.Method);
        }

        return result;
    }

    public async Task SendAsync(
        string providerName,
        HttpClient httpClient,
        HttpRequestMessage request,
        ExternalApiRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        using var response = await SendRawAsync(providerName, httpClient, request, options, cancellationToken);
    }

    public async Task<ExternalApiResult<TResponse>> TrySendAsync<TResponse>(
        string providerName,
        HttpClient httpClient,
        HttpRequestMessage request,
        ExternalApiRequestOptions? options = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var value = await SendAsync<TResponse>(providerName, httpClient, request, options, cancellationToken);
            return ExternalApiResult<TResponse>.Success(value);
        }
        catch (ExternalApiException ex)
        {
            return ExternalApiResult<TResponse>.Failure(new ExternalApiError(
                ex.ProviderName,
                ex.ErrorCode,
                ex.Message,
                ex.StatusCode,
                null));
        }
    }

    private async Task<HttpResponseMessage> SendRawAsync(
        string providerName,
        HttpClient httpClient,
        HttpRequestMessage request,
        ExternalApiRequestOptions? options,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(request);

        ApplyRequestOptions(providerName, request, options);

        var response = await httpClient.SendAsync(
            request,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);

        if (response.IsSuccessStatusCode)
            return response;

        var responseBody = response.Content is null
            ? null
            : await response.Content.ReadAsStringAsync(cancellationToken);
        var error = await _errorParser.ParseAsync(providerName, response, responseBody, cancellationToken);

        response.Dispose();

        throw new ExternalApiException(
            providerName,
            error.Message ?? $"External API '{providerName}' returned HTTP {(int)response.StatusCode}.",
            statusCode: response.StatusCode,
            errorCode: error.Code,
            responseBody: responseBody,
            requestUri: request.RequestUri,
            method: request.Method.Method,
            isTransient: IsTransient(response.StatusCode));
    }

    private static void ApplyRequestOptions(
        string providerName,
        HttpRequestMessage request,
        ExternalApiRequestOptions? options)
    {
        request.Options.Set(ExternalHttpRequestOptions.ProviderName, providerName);

        if (!string.IsNullOrWhiteSpace(options?.OperationName))
            request.Options.Set(ExternalHttpRequestOptions.OperationName, options.OperationName);

        if (options?.IsIdempotent is not null)
            request.Options.Set(ExternalHttpRequestOptions.IsIdempotent, options.IsIdempotent.Value);

        if (options?.Headers is null)
            return;

        foreach (var header in options.Headers)
        {
            request.Headers.TryAddWithoutValidation(header.Key, header.Value);
        }
    }

    private static bool IsTransient(System.Net.HttpStatusCode statusCode)
    {
        return statusCode is
            System.Net.HttpStatusCode.RequestTimeout or
            System.Net.HttpStatusCode.TooManyRequests or
            System.Net.HttpStatusCode.InternalServerError or
            System.Net.HttpStatusCode.BadGateway or
            System.Net.HttpStatusCode.ServiceUnavailable or
            System.Net.HttpStatusCode.GatewayTimeout;
    }
}
