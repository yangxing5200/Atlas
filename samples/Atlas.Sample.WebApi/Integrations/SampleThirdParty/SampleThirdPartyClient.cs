using Atlas.Infrastructure.Http.Abstractions;

namespace Atlas.Sample.WebApi.Integrations.SampleThirdParty;

public sealed class SampleThirdPartyClient : ISampleThirdPartyClient
{
    public const string ClientName = "SampleThirdParty";

    private readonly HttpClient _httpClient;
    private readonly IExternalApiExecutor _executor;

    public SampleThirdPartyClient(
        HttpClient httpClient,
        IExternalApiExecutor executor)
    {
        _httpClient = httpClient;
        _executor = executor;
    }

    public string ProviderName => ClientName;

    public Task<SampleThirdPartyProductDto> GetSupplierProductAsync(
        string sku,
        bool unstable = false,
        CancellationToken cancellationToken = default)
    {
        var path = $"/api/mock-third-party/supplier-catalog/products/{Uri.EscapeDataString(sku)}";
        if (unstable)
            path += "?unstable=true";

        using var request = new HttpRequestMessage(HttpMethod.Get, path);

        return _executor.SendAsync<SampleThirdPartyProductDto>(
            ProviderName,
            _httpClient,
            request,
            new ExternalApiRequestOptions
            {
                OperationName = "SampleThirdParty.GetSupplierProduct",
                IsIdempotent = true
            },
            cancellationToken);
    }
}
