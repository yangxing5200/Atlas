using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.BackgroundTasks;

/// <summary>
/// 后台任务运行时的依赖注入入口。
/// </summary>
public static class BackgroundTaskServiceCollectionExtensions
{
    public static IServiceCollection AddAtlasBackgroundTaskRuntime(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<RecurringTaskRunnerOptions>(configuration.GetSection("BackgroundTasks:Recurring"));
        services.Configure<BackgroundJobWorkerOptions>(configuration.GetSection("BackgroundTasks:OneTimeJobs"));

        // 入队客户端始终注册；是否启动 Worker 由配置控制，便于 Web/API 节点只写入任务不消费。
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
