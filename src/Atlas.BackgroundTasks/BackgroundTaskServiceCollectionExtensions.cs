using Microsoft.Extensions.Configuration;
using Atlas.BackgroundTasks.Operations;
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
        IConfiguration configuration,
        bool enableRecurringTaskRunnerByDefault = false,
        bool enableBackgroundJobWorkerByDefault = false)
    {
        var recurringSection = configuration.GetSection("BackgroundTasks:Recurring");
        var workerSection = configuration.GetSection("BackgroundTasks:OneTimeJobs");
        var recurringEnabledConfigured = HasConfiguredValue(recurringSection, nameof(RecurringTaskRunnerOptions.Enabled));
        var workerEnabledConfigured = HasConfiguredValue(workerSection, nameof(BackgroundJobWorkerOptions.Enabled));

        var recurringOptions = new RecurringTaskRunnerOptions();
        recurringSection.Bind(recurringOptions);
        if (!recurringEnabledConfigured)
            recurringOptions.Enabled = enableRecurringTaskRunnerByDefault;

        var workerOptions = new BackgroundJobWorkerOptions();
        workerSection.Bind(workerOptions);
        if (!workerEnabledConfigured)
            workerOptions.Enabled = enableBackgroundJobWorkerByDefault;
        ApplyMissingWorkerDefaults(workerSection, workerOptions);
        NormalizeBackgroundJobWorkerOptions(workerOptions);

        services.AddOptions<RecurringTaskRunnerOptions>()
            .Configure(options =>
            {
                Copy(recurringOptions, options);
            })
            .Validate(ValidateRecurringTaskRunnerOptions, "BackgroundTasks:Recurring is invalid.")
            .ValidateOnStart();

        services.AddOptions<BackgroundJobWorkerOptions>()
            .Configure(options =>
            {
                Copy(workerOptions, options);
            })
            .Validate(ValidateBackgroundJobWorkerOptions, "BackgroundTasks:OneTimeJobs is invalid.")
            .ValidateOnStart();
        services.PostConfigure<BackgroundJobWorkerOptions>(NormalizeBackgroundJobWorkerOptions);

        // 入队客户端始终注册；是否启动 Worker 由配置控制，便于 Web/API 节点只写入任务不消费。
        services.TryAddScoped<IBackgroundJobClient, BackgroundJobClient>();
        services.TryAddScoped<IBackgroundJobOperationsService, BackgroundJobOperationsService>();
        services.TryAddScoped<IBackgroundWorkerOperationsService, BackgroundWorkerOperationsService>();
        services.TryAddSingleton<IBackgroundJobProgressReporter, BackgroundJobProgressReporter>();
        services.TryAddSingleton<ISensitiveJsonMasker, SensitiveJsonMasker>();
        services.TryAddSingleton<BackgroundWorkerHeartbeatState>();

        if (recurringOptions.Enabled)
        {
            services.AddHostedService<RecurringTaskRunner>();
        }

        if (workerOptions.Enabled)
        {
            services.AddHostedService<BackgroundJobWorker>();
        }

        if (recurringOptions.Enabled || workerOptions.Enabled)
        {
            services.AddHostedService<BackgroundWorkerHeartbeatService>();
        }

        return services;
    }

    private static bool HasConfiguredValue(IConfigurationSection section, string key)
    {
        return section.GetChildren().Any(child => string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase));
    }

    private static void NormalizeBackgroundJobWorkerOptions(BackgroundJobWorkerOptions options)
    {
        var queues = options.Queues?
            .Where(queue => !string.IsNullOrWhiteSpace(queue))
            .Select(queue => queue.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];

        if (queues.Length == 0)
        {
            options.Queues = [BackgroundJobQueues.Default];
        }
        else
        {
            options.Queues = queues;
        }

        options.MaxConcurrency = Math.Max(1, options.MaxConcurrency);
        options.JobTypeConcurrency = (options.JobTypeConcurrency ?? new Dictionary<string, int>())
            .Where(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)
            .ToDictionary(
                x => x.Key.Trim(),
                x => Math.Max(1, x.Value),
                StringComparer.OrdinalIgnoreCase);
        options.IncludedJobTypes = NormalizeStringArray(options.IncludedJobTypes);
        options.ExcludedJobTypes = NormalizeStringArray(options.ExcludedJobTypes);
    }

    private static string[] NormalizeStringArray(string[]? values)
    {
        return values?
            .Where(value => !string.IsNullOrWhiteSpace(value))
            .Select(value => value.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray() ?? [];
    }

    private static void ApplyMissingWorkerDefaults(
        IConfigurationSection section,
        BackgroundJobWorkerOptions options)
    {
        var defaults = new BackgroundJobWorkerOptions();

        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.PollIntervalSeconds)))
            options.PollIntervalSeconds = defaults.PollIntervalSeconds;
        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.BatchSize)))
            options.BatchSize = defaults.BatchSize;
        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.MaxConcurrency)))
            options.MaxConcurrency = defaults.MaxConcurrency;
        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.ProcessingTimeoutSeconds)))
            options.ProcessingTimeoutSeconds = defaults.ProcessingTimeoutSeconds;
        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.MaxRunningSeconds)))
            options.MaxRunningSeconds = defaults.MaxRunningSeconds;
        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.CancellationCheckIntervalSeconds)))
            options.CancellationCheckIntervalSeconds = defaults.CancellationCheckIntervalSeconds;
        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.InitialRetryDelaySeconds)))
            options.InitialRetryDelaySeconds = defaults.InitialRetryDelaySeconds;
        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.MaxRetryDelaySeconds)))
            options.MaxRetryDelaySeconds = defaults.MaxRetryDelaySeconds;
        if (!HasConfiguredValue(section, nameof(BackgroundJobWorkerOptions.DefaultMaxAttempts)))
            options.DefaultMaxAttempts = defaults.DefaultMaxAttempts;
    }

    private static void Copy(
        RecurringTaskRunnerOptions source,
        RecurringTaskRunnerOptions target)
    {
        target.Enabled = source.Enabled;
        target.PollIntervalSeconds = source.PollIntervalSeconds;
        target.LockSeconds = source.LockSeconds;
    }

    private static void Copy(
        BackgroundJobWorkerOptions source,
        BackgroundJobWorkerOptions target)
    {
        target.Enabled = source.Enabled;
        target.Queues = source.Queues;
        target.PollIntervalSeconds = source.PollIntervalSeconds;
        target.BatchSize = source.BatchSize;
        target.MaxConcurrency = source.MaxConcurrency;
        target.JobTypeConcurrency = new Dictionary<string, int>(
            source.JobTypeConcurrency ?? new Dictionary<string, int>(),
            StringComparer.OrdinalIgnoreCase);
        target.IncludedJobTypes = source.IncludedJobTypes;
        target.ExcludedJobTypes = source.ExcludedJobTypes;
        target.ProcessingTimeoutSeconds = source.ProcessingTimeoutSeconds;
        target.MaxRunningSeconds = source.MaxRunningSeconds;
        target.CancellationCheckIntervalSeconds = source.CancellationCheckIntervalSeconds;
        target.InitialRetryDelaySeconds = source.InitialRetryDelaySeconds;
        target.MaxRetryDelaySeconds = source.MaxRetryDelaySeconds;
        target.DefaultMaxAttempts = source.DefaultMaxAttempts;
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
               options.MaxConcurrency > 0 &&
               (options.JobTypeConcurrency == null ||
                options.JobTypeConcurrency.All(x => !string.IsNullOrWhiteSpace(x.Key) && x.Value > 0)) &&
               (options.IncludedJobTypes == null ||
                options.IncludedJobTypes.All(x => !string.IsNullOrWhiteSpace(x))) &&
               (options.ExcludedJobTypes == null ||
                options.ExcludedJobTypes.All(x => !string.IsNullOrWhiteSpace(x))) &&
               options.ProcessingTimeoutSeconds > 0 &&
               options.MaxRunningSeconds > 0 &&
               options.CancellationCheckIntervalSeconds > 0 &&
               options.InitialRetryDelaySeconds > 0 &&
               options.MaxRetryDelaySeconds >= options.InitialRetryDelaySeconds &&
               options.DefaultMaxAttempts > 0;
    }
}
