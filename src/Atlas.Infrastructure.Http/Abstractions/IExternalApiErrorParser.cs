namespace Atlas.Infrastructure.Http.Abstractions;

public interface IExternalApiErrorParser
{
    ValueTask<ExternalApiError> ParseAsync(
        string providerName,
        HttpResponseMessage response,
        string? responseBody,
        CancellationToken cancellationToken = default);
}
