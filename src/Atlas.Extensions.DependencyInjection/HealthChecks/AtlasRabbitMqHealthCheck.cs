using System.Net.Sockets;
using Atlas.Messaging.RabbitMQ;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Atlas.Extensions.DependencyInjection.HealthChecks;

public sealed class AtlasRabbitMqHealthCheck : IHealthCheck
{
    private readonly IOptions<AtlasMessagingOptions> _messagingOptions;
    private readonly IOptions<AtlasRuntimeModeOptions> _runtimeOptions;
    private readonly IOptions<RabbitMqMessagingOptions> _rabbitMqOptions;

    public AtlasRabbitMqHealthCheck(
        IOptions<AtlasMessagingOptions> messagingOptions,
        IOptions<AtlasRuntimeModeOptions> runtimeOptions,
        IOptions<RabbitMqMessagingOptions> rabbitMqOptions)
    {
        _messagingOptions = messagingOptions ?? throw new ArgumentNullException(nameof(messagingOptions));
        _runtimeOptions = runtimeOptions ?? throw new ArgumentNullException(nameof(runtimeOptions));
        _rabbitMqOptions = rabbitMqOptions ?? throw new ArgumentNullException(nameof(rabbitMqOptions));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!IsRabbitMqProvider(_messagingOptions.Value.Provider))
            return HealthCheckResult.Healthy("RabbitMQ is not required for the configured messaging provider.");

        var runtime = _runtimeOptions.Value;
        if (!runtime.ShouldEnableMessagingConsumers() && !runtime.ShouldEnableTenantOutboxDispatcher())
            return HealthCheckResult.Healthy("RabbitMQ is configured but not required by this runtime mode.");

        var (host, port) = ResolveEndpoint(_rabbitMqOptions.Value);
        if (string.IsNullOrWhiteSpace(host) || port <= 0)
            return HealthCheckResult.Unhealthy("RabbitMQ host or port is invalid.");

        try
        {
            using var client = new TcpClient();
            await client.ConnectAsync(host, port, cancellationToken);
            return HealthCheckResult.Healthy("RabbitMQ TCP endpoint is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("RabbitMQ health check failed.", ex);
        }
    }

    private static bool IsRabbitMqProvider(string? provider)
    {
        var normalized = string.IsNullOrWhiteSpace(provider)
            ? "none"
            : provider.Trim().ToLowerInvariant();

        return normalized is "rabbitmq" or "rabbit-mq" or "rabbit";
    }

    private static (string Host, int Port) ResolveEndpoint(RabbitMqMessagingOptions options)
    {
        if (!string.IsNullOrWhiteSpace(options.Uri) &&
            Uri.TryCreate(options.Uri, UriKind.Absolute, out var uri))
        {
            return (uri.Host, uri.Port > 0 ? uri.Port : 5672);
        }

        return (options.Host, options.Port);
    }
}
