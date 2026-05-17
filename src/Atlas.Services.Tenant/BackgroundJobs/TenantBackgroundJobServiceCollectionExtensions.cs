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
        services.Configure<TenantOutboxMaintenanceOptions>(
            configuration.GetSection("BackgroundTasks:TenantOutboxMaintenance"));

        services.TryAddEnumerable(ServiceDescriptor.Scoped<IBackgroundJobHandler, TenantCacheWarmupJobHandler>());
        services.TryAddEnumerable(ServiceDescriptor.Scoped<IRecurringTask, TenantOutboxMaintenanceTask>());

        return services;
    }
}
