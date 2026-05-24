using Atlas.Core.Configuration;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Identity;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Locking;
using Atlas.Messaging.Abstractions;
using Atlas.Messaging.RabbitMQ;
using Atlas.Services.Tenant;
using Atlas.Services.Tenant.BackgroundJobs;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using System.Reflection;
using Atlas.BackgroundTasks;

namespace Atlas.Extensions.DependencyInjection;

/// <summary>
/// Provides extension methods for registering Atlas core services.
/// </summary>
public static class AtlasCoreServiceExtensions
{
    private const string DefaultCacheProvider = "memory";
    private const string GlobalConnectionStringKey = "AtlasGlobal";
    private const long DefaultDatacenterId = 1;
    private const int MaxWorkerId = 31;

    /// <summary>
    /// Registers all Atlas core services including infrastructure and business layers.
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddAtlasCore(
            configuration,
            AtlasModuleCatalog.CreateWithBuiltInModules(Array.Empty<IAtlasModule>()),
            Array.Empty<Assembly>());
    }

    /// <summary>
    /// Registers all Atlas core services and optional MassTransit consumer assemblies.
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] messagingConsumerAssemblies)
    {
        return services.AddAtlasCore(
            configuration,
            AtlasModuleCatalog.CreateWithBuiltInModules(Array.Empty<IAtlasModule>()),
            messagingConsumerAssemblies);
    }

    /// <summary>
    /// Registers all Atlas core services and modules.
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        params IAtlasModule[] modules)
    {
        return services.AddAtlasCore(configuration, modules.AsEnumerable());
    }

    /// <summary>
    /// Registers all Atlas core services and modules.
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<IAtlasModule> modules)
    {
        return services.AddAtlasCore(configuration, modules, Array.Empty<Assembly>());
    }

    /// <summary>
    /// Registers all Atlas core services and discovers modules from a registration callback.
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AtlasModuleRegistrationOptions> configureModules)
    {
        return services.AddAtlasCore(configuration, configureModules, Array.Empty<Assembly>());
    }

    /// <summary>
    /// Registers all Atlas core services, discovered modules, and optional MassTransit consumer assemblies.
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AtlasModuleRegistrationOptions> configureModules,
        params Assembly[] messagingConsumerAssemblies)
    {
        ArgumentNullException.ThrowIfNull(configureModules);

        var moduleOptions = new AtlasModuleRegistrationOptions();
        configureModules(moduleOptions);

        return services.AddAtlasCore(
            configuration,
            moduleOptions.BuildModules(),
            messagingConsumerAssemblies);
    }

    /// <summary>
    /// Registers all Atlas core services, modules, and optional MassTransit consumer assemblies.
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        IEnumerable<IAtlasModule> modules,
        params Assembly[] messagingConsumerAssemblies)
    {
        return services.AddAtlasCore(
            configuration,
            AtlasModuleCatalog.CreateWithBuiltInModules(modules),
            messagingConsumerAssemblies);
    }

    private static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        AtlasModuleCatalog moduleCatalog,
        Assembly[] messagingConsumerAssemblies)
    {
        // 注册顺序体现运行时依赖：身份、缓存和消息等基础设施先于业务服务。
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAtlasRuntimeOptions(configuration);
        services.AddAtlasSnowflakeId(configuration);
        services.AddAtlasDatabase(configuration);
        services.AddAtlasIdentity();
        services.AddAtlasCache(configuration);
        services.AddAtlasMessaging(
            configuration,
            CombineAssemblies(messagingConsumerAssemblies, moduleCatalog.ConsumerAssemblies));
        moduleCatalog.AddServices(services, configuration);
        services.AddAtlasModuleMapping(moduleCatalog.AutoMapperAssemblies);
        services.AddAtlasBackgroundTasks(configuration);

        return services;
    }

    private static IServiceCollection AddAtlasRuntimeOptions(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddOptions<AtlasCacheSettingsOptions>()
            .Bind(configuration.GetSection(AtlasCacheSettingsOptions.SectionName))
            .Validate(options => IsSupportedCacheProvider(options.Provider), "CacheSettings:Provider must be Memory, Redis, or Hybrid.")
            .Validate(
                options => !IsCacheProvider(options.Provider, "redis") ||
                           !string.IsNullOrWhiteSpace(options.Redis.ConnectionString),
                "CacheSettings:Redis:ConnectionString is required when CacheSettings:Provider is Redis.")
            .Validate(
                options => !IsCacheProvider(options.Provider, "hybrid") ||
                           !string.IsNullOrWhiteSpace(options.Hybrid.RedisConnectionString),
                "CacheSettings:Hybrid:RedisConnectionString is required when CacheSettings:Provider is Hybrid.")
            .Validate(
                options => !options.Hybrid.L1ExpirationMinutes.HasValue ||
                           options.Hybrid.L1ExpirationMinutes.Value > 0,
                "CacheSettings:Hybrid:L1ExpirationMinutes must be greater than 0 when configured.")
            .ValidateOnStart();

        services.AddOptions<AtlasMessagingOptions>()
            .Bind(configuration.GetSection(AtlasMessagingOptions.SectionName))
            .Validate(options => IsSupportedMessagingProvider(options.Provider), "Messaging:Provider must be None, NoOp, or RabbitMQ.")
            .ValidateOnStart();

        return services;
    }

    #region Infrastructure - HTTP Context

    private static IServiceCollection AddHttpContextAccessor(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        return services;
    }

    #endregion

    #region Infrastructure - Database

    /// <summary>
    /// Registers global database context with audit interceptor.
    /// </summary>
    private static IServiceCollection AddAtlasDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddScoped<AuditInterceptor>();

        var connectionString = configuration.GetConnectionString(GlobalConnectionStringKey)
            ?? throw new InvalidOperationException($"Connection string '{GlobalConnectionStringKey}' is required.");

        services.AddDbContext<AtlasGlobalDbContext>((sp, options) =>
        {
            var interceptor = sp.GetRequiredService<AuditInterceptor>();
            options.UseMySql(connectionString, ServerVersion.AutoDetect(connectionString))
                   .AddInterceptors(interceptor);
        });

        return services;
    }

    #endregion

    #region Infrastructure - Identity

    /// <summary>
    /// Registers current identity service for multi-tenant context.
    /// </summary>
    private static IServiceCollection AddAtlasIdentity(this IServiceCollection services)
    {
        services.AddScoped<ICurrentIdentity>(sp =>
        {
            var accessor = sp.GetRequiredService<IHttpContextAccessor>();
            return new CurrentIdentity(accessor);
        });

        return services;
    }

    #endregion

    #region Infrastructure - Cache

    /// <summary>
    /// Registers caching service based on configuration.
    /// Supports Memory, Redis, and Hybrid (L1+L2) strategies.
    /// </summary>
    private static IServiceCollection AddAtlasCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection(AtlasCacheSettingsOptions.SectionName).Get<AtlasCacheSettingsOptions>()
            ?? new AtlasCacheSettingsOptions();
        var provider = NormalizeProvider(options.Provider, DefaultCacheProvider);

        services.AddAtlasCaching();

        // 默认内存锁只适合单实例；Redis/数据库锁接入后应在这里替换为跨实例实现。
        services.TryAddSingleton<IDistributedLockProvider, MemoryDistributedLockProvider>();

        return provider switch
        {
            "redis" => services.AddAtlasRedisCache(options),
            "hybrid" => services.AddAtlasHybridCache(options),
            _ => services.AddMemoryCaching()
        };
    }

    private static IServiceCollection AddAtlasRedisCache(
        this IServiceCollection services,
        AtlasCacheSettingsOptions options)
    {
        var connectionString = options.Redis.ConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Redis connection string is required.");

        var instanceName = options.Redis.InstanceName;
        return services.AddRedisCaching(connectionString, instanceName);
    }

    private static IServiceCollection AddAtlasHybridCache(
        this IServiceCollection services,
        AtlasCacheSettingsOptions options)
    {
        var connectionString = options.Hybrid.RedisConnectionString;
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Hybrid cache Redis connection string is required.");

        return services.AddHybridCaching(connectionString, hybridOptions =>
        {
            var l1Minutes = options.Hybrid.L1ExpirationMinutes;
            if (l1Minutes.HasValue)
                hybridOptions.L1Expiration = TimeSpan.FromMinutes(l1Minutes.Value);
        });
    }

    #endregion

    #region Infrastructure - Messaging

    private static IServiceCollection AddAtlasMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly[] messagingConsumerAssemblies)
    {
        var options = configuration.GetSection(AtlasMessagingOptions.SectionName).Get<AtlasMessagingOptions>()
            ?? new AtlasMessagingOptions();
        var provider = NormalizeProvider(options.Provider, "none");

        if (provider is "rabbitmq" or "rabbit-mq" or "rabbit")
        {
            return services.AddAtlasRabbitMqMessaging(configuration, messagingConsumerAssemblies);
        }

        if (provider == "redis")
        {
            throw new NotSupportedException(
                "Redis Pub/Sub messaging has been removed. Use Messaging:Provider=RabbitMQ for reliable business messaging.");
        }

        if (provider is not "none" and not "noop")
            throw new InvalidOperationException($"Unsupported messaging provider '{provider}'.");

        // 未启用消息时使用 NoOp，保证领域服务可依赖发布接口而不关心部署形态。
        services.TryAddSingleton<IDomainEventPublisher, NoOpDomainEventPublisher>();
        services.TryAddSingleton<IDomainEventTransport, NoOpDomainEventTransport>();
        return services;
    }

    private static IServiceCollection AddAtlasRabbitMqMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly[] messagingConsumerAssemblies)
    {
        var rabbitMqSection = configuration.GetSection("Messaging:RabbitMQ");
        var options = rabbitMqSection.Get<RabbitMqMessagingOptions>() ?? new RabbitMqMessagingOptions();

        if (!ValidateRabbitMqMessagingOptions(options))
            throw new InvalidOperationException("Messaging:RabbitMQ is invalid.");

        services.AddOptions<RabbitMqMessagingOptions>()
            .Bind(rabbitMqSection)
            .Validate(ValidateRabbitMqMessagingOptions, "Messaging:RabbitMQ is invalid.")
            .ValidateOnStart();

        services.AddOptions<TenantOutboxDispatcherOptions>()
            .Bind(configuration.GetSection("Messaging:TenantOutbox"))
            .Validate(ValidateTenantOutboxDispatcherOptions, "Messaging:TenantOutbox is invalid.")
            .ValidateOnStart();

        services.AddMassTransit(configurator =>
        {
            configurator.SetKebabCaseEndpointNameFormatter();

            foreach (var assembly in messagingConsumerAssemblies.Where(x => x != null).Distinct())
            {
                configurator.AddConsumers(assembly);
            }

            // 全局 outbox 保护请求内发布，避免数据库提交成功但消息发布失败造成不一致。
            configurator.AddEntityFrameworkOutbox<AtlasGlobalDbContext>(outbox =>
            {
                outbox.UseMySql();
                outbox.UseBusOutbox();
            });

            configurator.UsingRabbitMq((context, cfg) =>
            {
                cfg.PrefetchCount = options.PrefetchCount;
                cfg.UseMessageRetry(retry =>
                    retry.Interval(
                        Math.Max(0, options.RetryLimit),
                        TimeSpan.FromSeconds(Math.Max(1, options.RetryIntervalSeconds))));

                if (!string.IsNullOrWhiteSpace(options.Uri))
                {
                    cfg.Host(new Uri(options.Uri), host =>
                    {
                        host.Username(options.Username);
                        host.Password(options.Password);
                    });
                }
                else
                {
                    cfg.Host(options.Host, options.Port, options.VirtualHost, host =>
                    {
                        host.Username(options.Username);
                        host.Password(options.Password);
                    });
                }

                cfg.ConfigureEndpoints(context);
            });
        });

        // Publisher 使用 Scoped IPublishEndpoint；Transport 使用 Singleton IBus 供后台 outbox 分发。
        services.RemoveAll<IDomainEventPublisher>();
        services.AddScoped<IDomainEventPublisher, MassTransitDomainEventPublisher>();
        services.RemoveAll<IDomainEventTransport>();
        services.AddSingleton<IDomainEventTransport, MassTransitDomainEventTransport>();
        services.AddHostedService<TenantOutboxDispatcher>();
        return services;
    }

    #endregion

    #region Background Tasks

    private static IServiceCollection AddAtlasBackgroundTasks(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services
            .AddAtlasBackgroundTaskRuntime(configuration)
            .AddAtlasTenantBackgroundJobs(configuration);
    }

    #endregion

    #region Infrastructure - ID Generator

    /// <summary>
    /// Registers Snowflake ID generator from configuration.
    /// Falls back to environment variables or auto-detection if config is missing.
    /// </summary>
    public static IServiceCollection AddAtlasSnowflakeId(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var options = configuration.GetSection("Snowflake").Get<SnowflakeOptions>()
            ?? GetDefaultSnowflakeOptions();

        options.Validate();

        services.AddOptions<SnowflakeOptions>()
            .Configure(opt =>
            {
                opt.WorkerId = options.WorkerId;
                opt.DatacenterId = options.DatacenterId;
            })
            .Validate(ValidateSnowflakeOptions, "Snowflake WorkerId and DatacenterId must be in the 0-31 range.")
            .ValidateOnStart();

        services.TryAddSingleton<IIdGenerator>(
            new SnowflakeIdGenerator(options.WorkerId, options.DatacenterId));

        return services;
    }

    /// <summary>
    /// Registers Snowflake ID generator with explicit worker and datacenter IDs.
    /// </summary>
    public static IServiceCollection AddAtlasSnowflakeId(
        this IServiceCollection services,
        long workerId,
        long datacenterId)
    {
        var options = new SnowflakeOptions { WorkerId = workerId, DatacenterId = datacenterId };
        options.Validate();

        services.TryAddSingleton<IIdGenerator>(
            new SnowflakeIdGenerator(workerId, datacenterId));

        return services;
    }

    /// <summary>
    /// Registers Snowflake ID generator with auto-detected worker ID.
    /// Worker ID is derived from environment variables or machine name hash.
    /// </summary>
    public static IServiceCollection AddAtlasSnowflakeIdAuto(
        this IServiceCollection services,
        long datacenterId = DefaultDatacenterId)
    {
        return services.AddAtlasSnowflakeId(GetAutoWorkerId(), datacenterId);
    }

    /// <summary>
    /// Registers Snowflake ID generator with runtime-resolved configuration.
    /// </summary>
    public static IServiceCollection AddAtlasSnowflakeId(
        this IServiceCollection services,
        Func<IServiceProvider, (long workerId, long datacenterId)> optionsFactory)
    {
        services.TryAddSingleton<IIdGenerator>(sp =>
        {
            var (workerId, datacenterId) = optionsFactory(sp);
            return new SnowflakeIdGenerator(workerId, datacenterId);
        });

        return services;
    }

    /// <summary>
    /// Resolves Snowflake options from environment variables or auto-generation.
    /// Priority: ENV vars > Machine name hash.
    /// </summary>
    private static SnowflakeOptions GetDefaultSnowflakeOptions()
    {
        var envWorkerId = Environment.GetEnvironmentVariable("SNOWFLAKE_WORKER_ID");
        var envDatacenterId = Environment.GetEnvironmentVariable("SNOWFLAKE_DATACENTER_ID");

        if (long.TryParse(envWorkerId, out var workerId) &&
            long.TryParse(envDatacenterId, out var datacenterId))
        {
            return new SnowflakeOptions { WorkerId = workerId, DatacenterId = datacenterId };
        }

        return new SnowflakeOptions
        {
            WorkerId = GetAutoWorkerId(),
            DatacenterId = DefaultDatacenterId
        };
    }

    /// <summary>
    /// Generates worker ID from environment variable or machine name hash (0-31).
    /// </summary>
    private static long GetAutoWorkerId()
    {
        var envWorkerId = Environment.GetEnvironmentVariable("SNOWFLAKE_WORKER_ID");

        if (long.TryParse(envWorkerId, out var workerId) && workerId is >= 0 and <= MaxWorkerId)
            return workerId;

        return Math.Abs(Environment.MachineName.GetHashCode()) % (MaxWorkerId + 1);
    }

    private static string NormalizeProvider(string? provider, string fallback)
    {
        return string.IsNullOrWhiteSpace(provider)
            ? fallback
            : provider.Trim().ToLowerInvariant();
    }

    private static Assembly[] CombineAssemblies(
        IEnumerable<Assembly>? first,
        IEnumerable<Assembly>? second)
    {
        return (first ?? Array.Empty<Assembly>())
            .Concat(second ?? Array.Empty<Assembly>())
            .Where(assembly => assembly is not null)
            .Distinct()
            .ToArray();
    }

    private static IServiceCollection AddAtlasModuleMapping(
        this IServiceCollection services,
        IEnumerable<Assembly> assemblies)
    {
        var mapperAssemblies = assemblies
            .Where(assembly => assembly is not null)
            .Distinct()
            .ToArray();

        if (mapperAssemblies.Length > 0)
            services.AddAutoMapper(_ => { }, mapperAssemblies);

        return services;
    }

    private static bool IsSupportedCacheProvider(string? provider)
    {
        var normalized = NormalizeProvider(provider, DefaultCacheProvider);
        return normalized is "memory" or "redis" or "hybrid";
    }

    private static bool IsCacheProvider(string? provider, string expected)
    {
        return NormalizeProvider(provider, DefaultCacheProvider) == expected;
    }

    private static bool IsSupportedMessagingProvider(string? provider)
    {
        var normalized = NormalizeProvider(provider, "none");
        return normalized is "none" or "noop" or "rabbitmq" or "rabbit-mq" or "rabbit";
    }

    private static bool ValidateRabbitMqMessagingOptions(RabbitMqMessagingOptions options)
    {
        return (!string.IsNullOrWhiteSpace(options.Uri) || !string.IsNullOrWhiteSpace(options.Host)) &&
               !string.IsNullOrWhiteSpace(options.VirtualHost) &&
               !string.IsNullOrWhiteSpace(options.Username) &&
               !string.IsNullOrWhiteSpace(options.Password) &&
               options.Port > 0 &&
               options.PrefetchCount > 0 &&
               options.RetryLimit >= 0 &&
               options.RetryIntervalSeconds > 0;
    }

    private static bool ValidateTenantOutboxDispatcherOptions(TenantOutboxDispatcherOptions options)
    {
        return options.PollIntervalSeconds > 0 &&
               options.TenantBatchSize > 0 &&
               options.MessageBatchSize > 0 &&
               options.MaxAttempts > 0 &&
               options.InitialRetryDelaySeconds > 0 &&
               options.MaxRetryDelaySeconds >= options.InitialRetryDelaySeconds &&
               options.ProcessingTimeoutSeconds > 0;
    }

    private static bool ValidateSnowflakeOptions(SnowflakeOptions options)
    {
        return options.WorkerId is >= 0 and <= MaxWorkerId &&
               options.DatacenterId is >= 0 and <= MaxWorkerId;
    }

    #endregion
}
