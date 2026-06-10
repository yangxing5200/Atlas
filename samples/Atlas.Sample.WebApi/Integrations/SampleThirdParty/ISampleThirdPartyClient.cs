using Atlas.Infrastructure.Http.Abstractions;

namespace Atlas.Sample.WebApi.Integrations.SampleThirdParty;

public interface ISampleThirdPartyClient : IExternalApiClient
{
    Task<SampleThirdPartyProductDto> GetSupplierProductAsync(
        string sku,
        bool unstable = false,
        CancellationToken cancellationToken = default);
}
