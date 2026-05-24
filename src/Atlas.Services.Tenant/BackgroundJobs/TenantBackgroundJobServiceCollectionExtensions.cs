using Atlas.BackgroundTasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Services.Tenant.BackgroundJobs;

public static class TenantBackgroundJobServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasTenantBackgroundJobs(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<TenantOutboxMaintenanceOptions>()
            .Bind(configuration.GetSection("BackgroundTasks:TenantOutboxMaintenance"))
            .Validate(ValidateTenantOutboxMaintenanceOptions, "BackgroundTasks:TenantOutboxMaintenance is invalid.")
            .ValidateOnStart();

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBackgroundJobHandler, TenantCacheWarmupJobHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecurringTask, TenantOutboxMaintenanceTask>());

        return services;
    }

    private static bool ValidateTenantOutboxMaintenanceOptions(TenantOutboxMaintenanceOptions options)
    {
        return options.IntervalMinutes > 0 &&
               options.RetentionDays > 0 &&
               options.TenantBatchSize > 0 &&
               options.DeleteBatchSize > 0;
    }
}
