using Atlas.Infrastructure.Http.Abstractions;
using Atlas.Infrastructure.Http.Configuration;
using Atlas.Infrastructure.Http.Handlers;
using Atlas.Infrastructure.Http.Internal;
using Atlas.Infrastructure.Http.Resilience;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Atlas.Infrastructure.Http.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasHttp(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ExternalHttpOptions>()
            .Bind(configuration.GetSection(ExternalHttpOptions.SectionName));

        services.TryAddEnumerable(ServiceDescriptor.Singleton<IValidateOptions<ExternalHttpOptions>, ExternalHttpOptionsValidator>());
        services.TryAddSingleton<IExternalHttpClientOptionsResolver, ExternalHttpClientOptionsResolver>();
        services.TryAddSingleton<IExternalApiErrorParser, DefaultExternalApiErrorParser>();
        services.TryAddSingleton<IExternalApiExecutor, ExternalApiExecutor>();
        services.TryAddSingleton<ExternalHttpResilienceStateRegistry>();

        return services;
    }

    public static IHttpClientBuilder AddAtlasExternalHttpClient<TClient, TImplementation>(
        this IServiceCollection services,
        IConfiguration configuration,
        string clientName,
        Action<ExternalHttpClientOptions>? configure = null)
        where TClient : class
        where TImplementation : class, TClient
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(clientName);

        services.AddAtlasHttp(configuration);

        if (configure is not null)
        {
            services.PostConfigure<ExternalHttpOptions>(options =>
            {
                options.Clients ??= new Dictionary<string, ExternalHttpClientOptions>(StringComparer.OrdinalIgnoreCase);

                if (!options.Clients.TryGetValue(clientName, out var clientOptions))
                {
                    clientOptions = new ExternalHttpClientOptions();
                    options.Clients[clientName] = clientOptions;
                }

                configure(clientOptions);
            });
        }

        var builder = services.AddHttpClient<TClient, TImplementation>((sp, httpClient) =>
        {
            var resolver = sp.GetRequiredService<IExternalHttpClientOptionsResolver>();
            var options = resolver.Get(clientName);

            if (!string.IsNullOrWhiteSpace(options.BaseUrl))
                httpClient.BaseAddress = new Uri(options.BaseUrl, UriKind.Absolute);

            httpClient.Timeout = options.Timeout;

            foreach (var header in options.DefaultHeaders)
            {
                if (!httpClient.DefaultRequestHeaders.Contains(header.Key))
                    httpClient.DefaultRequestHeaders.TryAddWithoutValidation(header.Key, header.Value);
            }
        });

        builder.AddHttpMessageHandler(sp => new ExternalHttpContextPropagationHandler(
            clientName,
            sp.GetRequiredService<IExternalHttpClientOptionsResolver>()));

        builder.AddHttpMessageHandler(sp => new ExternalHttpAuthenticationHandler(
            clientName,
            sp.GetRequiredService<IExternalHttpClientOptionsResolver>()));

        builder.AddHttpMessageHandler(sp => new ExternalHttpLoggingHandler(
            clientName,
            sp.GetRequiredService<IExternalHttpClientOptionsResolver>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ExternalHttpLoggingHandler>>()));

        builder.AddHttpMessageHandler(sp => new ExternalHttpResilienceHandler(
            clientName,
            sp.GetRequiredService<IExternalHttpClientOptionsResolver>(),
            sp.GetRequiredService<ExternalHttpResilienceStateRegistry>(),
            sp.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ExternalHttpResilienceHandler>>()));

        return builder;
    }
}
