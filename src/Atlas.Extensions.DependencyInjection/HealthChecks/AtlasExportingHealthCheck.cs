using Atlas.Exporting;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Microsoft.Extensions.Options;

namespace Atlas.Extensions.DependencyInjection.HealthChecks;

public sealed class AtlasExportingHealthCheck : IHealthCheck
{
    private readonly ExportJobOptions _options;

    public AtlasExportingHealthCheck(IOptions<ExportJobOptions> options)
    {
        _options = options?.Value ?? new ExportJobOptions();
    }

    public Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        if (!_options.Enabled)
            return Task.FromResult(HealthCheckResult.Healthy("Exporting is disabled."));

        if (_options.AllowedFormats.Length == 0)
            return Task.FromResult(HealthCheckResult.Unhealthy("Exporting has no allowed formats."));

        if (!string.Equals(_options.StorageProvider, "Local", StringComparison.OrdinalIgnoreCase))
            return Task.FromResult(HealthCheckResult.Unhealthy($"Unsupported export storage provider '{_options.StorageProvider}'."));

        return Task.FromResult(HealthCheckResult.Healthy("Exporting is configured."));
    }
}
