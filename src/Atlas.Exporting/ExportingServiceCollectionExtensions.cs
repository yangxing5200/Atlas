using Atlas.BackgroundTasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Exporting;

public static class ExportingServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasExporting(this IServiceCollection services, IConfiguration configuration)
    {
        var section = configuration.GetSection(ExportingOptions.SectionName);
        services.AddOptions<ExportingOptions>()
            .Bind(section)
            .Validate(ValidateOptions, "Exporting configuration is invalid.")
            .ValidateOnStart();

        services.TryAddScoped<IExportJobService, ExportJobService>();
        services.TryAddScoped<IExportProviderRegistry, ExportProviderRegistry>();
        services.TryAddScoped<IExportFileStore, LocalExportFileStore>();
        services.TryAddEnumerable(ServiceDescriptor.Singleton<IExportFormatWriter, CsvExportFormatWriter>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBackgroundJobHandler, ExportJobHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecurringTask, ExportArtifactCleanupTask>());
        return services;
    }

    private static bool ValidateOptions(ExportingOptions options)
    {
        return options.DefaultPageSize > 0 &&
               options.MaxPageSize >= options.DefaultPageSize &&
               options.RetentionDays > 0 &&
               options.AllowedFormats.Length > 0 &&
               options.AllowedFormats.All(x => !string.IsNullOrWhiteSpace(x)) &&
               !string.IsNullOrWhiteSpace(options.StorageProvider) &&
               string.Equals(options.StorageProvider, "Local", StringComparison.OrdinalIgnoreCase) &&
               !string.IsNullOrWhiteSpace(options.LocalStorage.RootPath) &&
               options.Cleanup.IntervalMinutes > 0 &&
               options.Cleanup.BatchSize > 0;
    }
}
