namespace Atlas.Infrastructure.Http.Abstractions;

public interface IExternalApiExecutor
{
    Task<TResponse> SendAsync<TResponse>(
        string providerName,
        HttpClient httpClient,
        HttpRequestMessage request,
        ExternalApiRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    Task SendAsync(
        string providerName,
        HttpClient httpClient,
        HttpRequestMessage request,
        ExternalApiRequestOptions? options = null,
        CancellationToken cancellationToken = default);

    Task<ExternalApiResult<TResponse>> TrySendAsync<TResponse>(
        string providerName,
        HttpClient httpClient,
        HttpRequestMessage request,
        ExternalApiRequestOptions? options = null,
        CancellationToken cancellationToken = default);
}
