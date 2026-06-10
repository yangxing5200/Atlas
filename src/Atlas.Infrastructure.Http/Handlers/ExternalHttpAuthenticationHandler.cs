using System.Net.Http.Headers;
using Atlas.Infrastructure.Http.Configuration;

namespace Atlas.Infrastructure.Http.Handlers;

public sealed class ExternalHttpAuthenticationHandler : DelegatingHandler
{
    private readonly string _clientName;
    private readonly IExternalHttpClientOptionsResolver _optionsResolver;

    public ExternalHttpAuthenticationHandler(
        string clientName,
        IExternalHttpClientOptionsResolver optionsResolver)
    {
        _clientName = clientName;
        _optionsResolver = optionsResolver;
    }

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var options = _optionsResolver.Get(_clientName).Authentication;

        if (!string.IsNullOrWhiteSpace(options.BearerToken) && request.Headers.Authorization is null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue(
                options.AuthorizationScheme,
                options.BearerToken);
        }

        if (!string.IsNullOrWhiteSpace(options.ApiKey) &&
            !string.IsNullOrWhiteSpace(options.ApiKeyHeaderName) &&
            !request.Headers.Contains(options.ApiKeyHeaderName))
        {
            request.Headers.TryAddWithoutValidation(options.ApiKeyHeaderName, options.ApiKey);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
