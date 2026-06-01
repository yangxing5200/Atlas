using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Atlas.Extensions.DependencyInjection.HealthChecks;

public sealed class AtlasRedisHealthCheck : IHealthCheck
{
    private readonly IOptions<AtlasCacheSettingsOptions> _cacheOptions;
    private readonly IConnectionMultiplexer? _redis;

    public AtlasRedisHealthCheck(
        IOptions<AtlasCacheSettingsOptions> cacheOptions,
        IConnectionMultiplexer? redis = null)
    {
        _cacheOptions = cacheOptions ?? throw new ArgumentNullException(nameof(cacheOptions));
        _redis = redis;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        var provider = NormalizeProvider(_cacheOptions.Value.Provider);
        if (provider is not "redis" and not "hybrid")
            return HealthCheckResult.Healthy("Redis is not required for the configured cache provider.");

        if (_redis == null)
            return HealthCheckResult.Unhealthy("Redis is required but no connection is registered.");

        try
        {
            await _redis.GetDatabase().PingAsync();
            return HealthCheckResult.Healthy("Redis is reachable.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Redis health check failed.", ex);
        }
    }

    private static string NormalizeProvider(string? provider)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? "memory"
            : provider.Trim().ToLowerInvariant();
    }
}
