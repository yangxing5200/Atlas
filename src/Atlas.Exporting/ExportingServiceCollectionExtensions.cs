using Atlas.BackgroundTasks;
using Atlas.Core.Services;
using Atlas.Exporting.Cleanup;
using Atlas.Exporting.Reconciliation;
using Atlas.Exporting.Storage;
using Atlas.Exporting.Writing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Exporting;

public static class ExportingServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasExporting(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        services.AddOptions<ExportJobOptions>()
            .Bind(configuration.GetSection(ExportJobOptions.SectionName))
            .Validate(ValidateExportJobOptions, "Exporting is invalid.")
            .ValidateOnStart();

        services.TryAddSingleton<IExecutionIdentityAccessor, ExecutionIdentityAccessor>();
        services.TryAddScoped<IExportJobService, ExportJobService>();
        services.TryAddScoped<IExportFileStore, LocalExportFileStore>();
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IExportFormatWriter, CsvExportFormatWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBackgroundJobHandler, ExportJobHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecurringTask, ExportArtifactCleanupTask>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecurringTask, ExportJobReconciliationTask>());

        return services;
    }

    private static bool ValidateExportJobOptions(ExportJobOptions options)
    {
        return options.DefaultPageSize > 0 &&
               options.MaxPageSize >= options.DefaultPageSize &&
               options.RetentionDays > 0 &&
               options.AllowedFormats.Length > 0 &&
               options.AllowedFormats.All(format => !string.IsNullOrWhiteSpace(format)) &&
               IsSupportedStorageProvider(options.StorageProvider) &&
               !string.IsNullOrWhiteSpace(options.DefaultFormat) &&
               !string.IsNullOrWhiteSpace(options.LocalStorage.RootPath) &&
               options.Cleanup.IntervalMinutes > 0 &&
               options.Cleanup.BatchSize > 0 &&
               options.Reconciliation.IntervalMinutes > 0 &&
               options.Reconciliation.StalePendingMinutes > 0 &&
               options.Reconciliation.StaleRunningMinutes > 0 &&
               options.Reconciliation.BatchSize > 0;
    }

    private static bool IsSupportedStorageProvider(string? provider)
    {
        return string.Equals(
            string.IsNullOrWhiteSpace(provider) ? "Local" : provider.Trim(),
            "Local",
            StringComparison.OrdinalIgnoreCase);
    }
}
