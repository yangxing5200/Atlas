using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.BackgroundTasks;

public static class BackgroundTaskServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasBackgroundTaskRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RecurringTaskRunnerOptions>(configuration.GetSection("BackgroundTasks:Recurring"));
        services.Configure<BackgroundJobWorkerOptions>(configuration.GetSection("BackgroundTasks:OneTimeJobs"));

        services.TryAddScoped<IBackgroundJobClient, BackgroundJobClient>();

        if (configuration.GetValue<bool>("BackgroundTasks:Recurring:Enabled"))
        {
            services.AddHostedService<RecurringTaskRunner>();
        }

        if (configuration.GetValue<bool>("BackgroundTasks:OneTimeJobs:Enabled"))
        {
            services.AddHostedService<BackgroundJobWorker>();
        }

        return services;
    }
}
