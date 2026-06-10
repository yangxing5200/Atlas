using System.Diagnostics;
using Atlas.Infrastructure.Http.Configuration;

namespace Atlas.Infrastructure.Http.Handlers;

public sealed class ExternalHttpContextPropagationHandler : DelegatingHandler
{
    private readonly string _clientName;
    private readonly IExternalHttpClientOptionsResolver _optionsResolver;

    public ExternalHttpContextPropagationHandler(
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
        var options = _optionsResolver.Get(_clientName).ContextPropagation;
        if (!options.Enabled)
            return base.SendAsync(request, cancellationToken);

        var traceId = Activity.Current?.TraceId.ToString();
        if (!string.IsNullOrWhiteSpace(traceId) &&
            !request.Headers.Contains(options.CorrelationHeaderName))
        {
            request.Headers.TryAddWithoutValidation(options.CorrelationHeaderName, traceId);
        }

        if (!string.IsNullOrWhiteSpace(traceId) &&
            !request.Headers.Contains(options.TraceIdHeaderName))
        {
            request.Headers.TryAddWithoutValidation(options.TraceIdHeaderName, traceId);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
