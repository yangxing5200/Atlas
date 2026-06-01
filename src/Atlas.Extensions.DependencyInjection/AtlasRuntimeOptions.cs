using Microsoft.Extensions.Configuration;

namespace Atlas.Extensions.DependencyInjection;

public enum AtlasRuntimeMode
{
    WebApi,
    Worker,
    Migration
}

public sealed class AtlasRuntimeModeOptions
{
    public const string SectionName = "Atlas:Runtime";

    public AtlasRuntimeMode Mode { get; set; } = AtlasRuntimeMode.WebApi;
    public bool? EnableMessagingConsumers { get; set; }
    public bool? EnableTenantOutboxDispatcher { get; set; }
    public bool? EnableBackgroundJobWorker { get; set; }
    public bool? EnableRecurringTaskRunner { get; set; }

    public static AtlasRuntimeModeOptions FromConfiguration(
        IConfiguration configuration,
        AtlasRuntimeMode defaultMode)
    {
        ArgumentNullException.ThrowIfNull(configuration);

        var section = configuration.GetSection(SectionName);
        var options = new AtlasRuntimeModeOptions
        {
            Mode = defaultMode,
            EnableMessagingConsumers = section.GetValue<bool?>(nameof(EnableMessagingConsumers)),
            EnableTenantOutboxDispatcher = section.GetValue<bool?>(nameof(EnableTenantOutboxDispatcher)),
            EnableBackgroundJobWorker = section.GetValue<bool?>(nameof(EnableBackgroundJobWorker)),
            EnableRecurringTaskRunner = section.GetValue<bool?>(nameof(EnableRecurringTaskRunner))
        };

        var configuredMode = section[nameof(Mode)];
        if (!string.IsNullOrWhiteSpace(configuredMode))
            options.Mode = ParseMode(configuredMode);

        return options;
    }

    public static AtlasRuntimeMode ParseMode(string? value)
    {
        var normalized = value?.Trim().Replace("-", string.Empty, StringComparison.Ordinal).ToLowerInvariant();
        return normalized switch
        {
            "webapi" or "web" or "http" => AtlasRuntimeMode.WebApi,
            "worker" or "background" => AtlasRuntimeMode.Worker,
            "migration" or "migrations" or "migrate" => AtlasRuntimeMode.Migration,
            _ => throw new InvalidOperationException(
                $"Atlas runtime mode '{value}' is invalid. Supported values are WebApi, Worker, and Migration.")
        };
    }

    public bool ShouldEnableMessagingConsumers()
    {
        return EnableMessagingConsumers ?? Mode == AtlasRuntimeMode.Worker;
    }

    public bool ShouldEnableTenantOutboxDispatcher()
    {
        return EnableTenantOutboxDispatcher ?? Mode == AtlasRuntimeMode.Worker;
    }

    public bool ShouldEnableBackgroundJobWorker()
    {
        return EnableBackgroundJobWorker ?? Mode == AtlasRuntimeMode.Worker;
    }

    public bool ShouldEnableRecurringTaskRunner()
    {
        return EnableRecurringTaskRunner ?? Mode == AtlasRuntimeMode.Worker;
    }
}

public sealed class AtlasCacheSettingsOptions
{
    public const string SectionName = "CacheSettings";

    public string Provider { get; set; } = "Memory";
    public AtlasRedisCacheOptions Redis { get; set; } = new();
    public AtlasHybridCacheOptions Hybrid { get; set; } = new();
}

public sealed class AtlasRedisCacheOptions
{
    public string? ConnectionString { get; set; }
    public string? InstanceName { get; set; }
}

public sealed class AtlasHybridCacheOptions
{
    public string? RedisConnectionString { get; set; }
    public int? L1ExpirationMinutes { get; set; }
}

public sealed class AtlasMessagingOptions
{
    public const string SectionName = "Messaging";

    public string Provider { get; set; } = "None";
}
