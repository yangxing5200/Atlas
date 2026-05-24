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
        var recurringOptions = configuration
            .GetSection("BackgroundTasks:Recurring")
            .Get<RecurringTaskRunnerOptions>() ?? new RecurringTaskRunnerOptions();
        var workerOptions = configuration
            .GetSection("BackgroundTasks:OneTimeJobs")
            .Get<BackgroundJobWorkerOptions>() ?? new BackgroundJobWorkerOptions();

        services.AddOptions<RecurringTaskRunnerOptions>()
            .Bind(configuration.GetSection("BackgroundTasks:Recurring"))
            .Validate(ValidateRecurringTaskRunnerOptions, "BackgroundTasks:Recurring is invalid.")
            .ValidateOnStart();

        services.AddOptions<BackgroundJobWorkerOptions>()
            .Bind(configuration.GetSection("BackgroundTasks:OneTimeJobs"))
            .Validate(ValidateBackgroundJobWorkerOptions, "BackgroundTasks:OneTimeJobs is invalid.")
            .ValidateOnStart();

        // 入队客户端始终注册；是否启动 Worker 由配置控制，便于 Web/API 节点只写入任务不消费。
        services.TryAddScoped<IBackgroundJobClient, BackgroundJobClient>();

        if (recurringOptions.Enabled)
        {
            services.AddHostedService<RecurringTaskRunner>();
        }

        if (workerOptions.Enabled)
        {
            services.AddHostedService<BackgroundJobWorker>();
        }

        return services;
    }

    private static bool ValidateRecurringTaskRunnerOptions(RecurringTaskRunnerOptions options)
    {
        return options.PollIntervalSeconds > 0 &&
               options.LockSeconds > 0;
    }

    private static bool ValidateBackgroundJobWorkerOptions(BackgroundJobWorkerOptions options)
    {
        return options.Queues.Length > 0 &&
               options.Queues.All(queue => !string.IsNullOrWhiteSpace(queue)) &&
               options.PollIntervalSeconds > 0 &&
               options.BatchSize > 0 &&
               options.ProcessingTimeoutSeconds > 0 &&
               options.InitialRetryDelaySeconds > 0 &&
               options.MaxRetryDelaySeconds >= options.InitialRetryDelaySeconds &&
               options.DefaultMaxAttempts > 0;
    }
}
