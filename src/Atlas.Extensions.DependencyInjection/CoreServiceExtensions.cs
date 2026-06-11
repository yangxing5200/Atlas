using Atlas.Core.Configuration;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Core.Telemetry;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Global;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Identity;
using Atlas.Exporting;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Locking;
using Atlas.Infrastructure.Http.Extensions;
using Atlas.Messaging.Abstractions;
using Atlas.Messaging.RabbitMQ;
using Atlas.Services.Tenant;
using Atlas.Services.Tenant.Runtime.BackgroundJobs;
using Atlas.Services.Tenant.Runtime.Messaging;
using MassTransit;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Exporter;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
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
            Array.Empty<Assembly>(),
            AtlasRuntimeMode.Worker);
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
            messagingConsumerAssemblies,
            AtlasRuntimeMode.Worker);
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
    /// Registers Atlas services for a Worker host. Worker mode enables messaging consumers and background execution by default.
    /// </summary>
    public static IServiceCollection AddAtlasWorker(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] messagingConsumerAssemblies)
    {
        return services.AddAtlasCore(
            configuration,
            AtlasModuleCatalog.CreateWithBuiltInModules(Array.Empty<IAtlasModule>()),
            messagingConsumerAssemblies,
            AtlasRuntimeMode.Worker);
    }

    /// <summary>
    /// Registers Atlas services for a Worker host with discovered modules.
    /// </summary>
    public static IServiceCollection AddAtlasWorker(
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
            AtlasModuleCatalog.CreateWithBuiltInModules(moduleOptions.BuildModules()),
            messagingConsumerAssemblies,
            AtlasRuntimeMode.Worker);
    }

    /// <summary>
    /// Registers Atlas services for a Migration host. Migration mode keeps background workers and consumers disabled by default.
    /// </summary>
    public static IServiceCollection AddAtlasMigration(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        return services.AddAtlasCore(
            configuration,
            AtlasModuleCatalog.CreateWithBuiltInModules(Array.Empty<IAtlasModule>()),
            Array.Empty<Assembly>(),
            AtlasRuntimeMode.Migration);
    }

    /// <summary>
    /// Registers Atlas services for a Migration host with explicit modules.
    /// </summary>
    public static IServiceCollection AddAtlasMigration(
        this IServiceCollection services,
        IConfiguration configuration,
        Action<AtlasModuleRegistrationOptions> configureModules)
    {
        ArgumentNullException.ThrowIfNull(configureModules);

        var moduleOptions = new AtlasModuleRegistrationOptions();
        configureModules(moduleOptions);

        return services.AddAtlasCore(
            configuration,
            AtlasModuleCatalog.CreateWithBuiltInModules(moduleOptions.BuildModules()),
            Array.Empty<Assembly>(),
            AtlasRuntimeMode.Migration);
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
        return services.AddAtlasCore(
            configuration,
            moduleCatalog,
            messagingConsumerAssemblies,
            AtlasRuntimeMode.Worker);
    }

    internal static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        AtlasModuleCatalog moduleCatalog,
        Assembly[] messagingConsumerAssemblies,
        AtlasRuntimeMode defaultRuntimeMode)
    {
        // 注册顺序体现运行时依赖：身份、缓存和消息等基础设施先于业务服务。
        services.AddLogging();
        services.AddHttpContextAccessor();
        var runtimeOptions = services.AddAtlasRuntimeOptions(configuration, defaultRuntimeMode);
        services.AddAtlasSnowflakeId(configuration);
        services.AddAtlasDatabase(configuration);
        services.AddAtlasIdentity();
        services.AddAtlasCache(configuration);
        services.AddAtlasHttp(configuration);
        services.AddAtlasMessagingRuntime(
            configuration,
            runtimeOptions,
            CombineAssemblies(messagingConsumerAssemblies, moduleCatalog.ConsumerAssemblies));
        services.RemoveAll<IAtlasTenantEntityConfigurationAssemblyProvider>();
        services.AddSingleton<IAtlasTenantEntityConfigurationAssemblyProvider>(
            new AtlasTenantEntityConfigurationAssemblyProvider(moduleCatalog.EntityConfigurationAssemblies));
        moduleCatalog.AddServices(services, configuration);
        services.AddAtlasModuleMapping(moduleCatalog.AutoMapperAssemblies);
        services.AddAtlasBackgroundTasks(configuration, runtimeOptions);
        services.AddAtlasOpenTelemetry(configuration);

        return services;
    }

    /// <summary>
    /// Registers Atlas runtime mode, cache, and messaging options and returns the resolved runtime defaults.
    /// </summary>
    public static AtlasRuntimeModeOptions AddAtlasRuntimeOptions(
        this IServiceCollection services,
        IConfiguration configuration,
        AtlasRuntimeMode defaultRuntimeMode)
    {
        var runtimeOptions = AtlasRuntimeModeOptions.FromConfiguration(configuration, defaultRuntimeMode);

        services.AddOptions<AtlasRuntimeModeOptions>()
            .Configure(options =>
            {
                options.Mode = runtimeOptions.Mode;
                options.EnableMessagingConsumers = runtimeOptions.EnableMessagingConsumers;
                options.EnableTenantOutboxDispatcher = runtimeOptions.EnableTenantOutboxDispatcher;
                options.EnableBackgroundJobWorker = runtimeOptions.EnableBackgroundJobWorker;
                options.EnableRecurringTaskRunner = runtimeOptions.EnableRecurringTaskRunner;
            })
            .ValidateOnStart();

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

        return runtimeOptions;
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
    public static IServiceCollection AddAtlasDatabase(
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
    public static IServiceCollection AddAtlasIdentity(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.TryAddSingleton<IExecutionIdentityAccessor, ExecutionIdentityAccessor>();
        services.AddScoped<ICurrentIdentity>(sp =>
        {
            var accessor = sp.GetRequiredService<IHttpContextAccessor>();
            var executionIdentityAccessor = sp.GetRequiredService<IExecutionIdentityAccessor>();
            return new CurrentIdentity(accessor, executionIdentityAccessor);
        });
        services.TryAddScoped<Atlas.Core.Context.ITenantExecutionContext>(sp => sp.GetRequiredService<ICurrentIdentity>());

        return services;
    }

    #endregion

    #region Infrastructure - Cache

    /// <summary>
    /// Registers caching service based on configuration.
    /// Supports Memory, Redis, and Hybrid (L1+L2) strategies.
    /// </summary>
    public static IServiceCollection AddAtlasCache(
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

    public static IServiceCollection AddAtlasMessagingRuntime(
        this IServiceCollection services,
        IConfiguration configuration,
        AtlasRuntimeModeOptions runtimeOptions,
        params Assembly[] messagingConsumerAssemblies)
    {
        var options = configuration.GetSection(AtlasMessagingOptions.SectionName).Get<AtlasMessagingOptions>()
            ?? new AtlasMessagingOptions();
        var provider = NormalizeProvider(options.Provider, "none");

        if (provider is "rabbitmq" or "rabbit-mq" or "rabbit")
        {
            var tenantOutboxOptions = configuration.GetSection("Messaging:TenantOutbox")
                .Get<TenantOutboxDispatcherOptions>() ?? new TenantOutboxDispatcherOptions();
            var tenantOutboxEnabled = HasConfiguredValue(configuration.GetSection("Messaging:TenantOutbox"), "Enabled")
                ? tenantOutboxOptions.Enabled
                : runtimeOptions.ShouldEnableTenantOutboxDispatcher();
            var consumersEnabled = runtimeOptions.ShouldEnableMessagingConsumers();

            if (!tenantOutboxEnabled && !consumersEnabled)
            {
                services.TryAddSingleton<IDomainEventPublisher, NoOpDomainEventPublisher>();
                services.TryAddSingleton<IDomainEventTransport, NoOpDomainEventTransport>();
                return services;
            }

            var enabledConsumerAssemblies = consumersEnabled
                ? messagingConsumerAssemblies
                : Array.Empty<Assembly>();

            return services.AddAtlasRabbitMqMessaging(
                configuration,
                enabledConsumerAssemblies,
                tenantOutboxEnabled);
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
        Assembly[] messagingConsumerAssemblies,
        bool enableTenantOutboxDispatcher)
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
        if (enableTenantOutboxDispatcher)
        {
            services.AddHostedService<TenantOutboxDispatcher>();
        }

        return services;
    }

    #endregion

    #region Background Tasks

    public static IServiceCollection AddAtlasBackgroundTasks(
        this IServiceCollection services,
        IConfiguration configuration,
        AtlasRuntimeModeOptions runtimeOptions)
    {
        return services
            .AddAtlasBackgroundTaskRuntime(
                configuration,
                runtimeOptions.ShouldEnableRecurringTaskRunner(),
                runtimeOptions.ShouldEnableBackgroundJobWorker())
            .AddAtlasTenantBackgroundJobs(configuration)
            .AddAtlasExporting(configuration);
    }

    #endregion

    #region Infrastructure - Observability

    public static IServiceCollection AddAtlasOpenTelemetry(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection(AtlasOpenTelemetryOptions.SectionName);
        var options = section.Get<AtlasOpenTelemetryOptions>() ?? new AtlasOpenTelemetryOptions();

        services.AddOptions<AtlasOpenTelemetryOptions>()
            .Bind(section)
            .Validate(ValidateOpenTelemetryOptions, $"{AtlasOpenTelemetryOptions.SectionName} is invalid.")
            .ValidateOnStart();

        if (!options.Enabled)
            return services;

        var serviceVersion = string.IsNullOrWhiteSpace(options.ServiceVersion)
            ? typeof(AtlasCoreServiceExtensions).Assembly.GetName().Version?.ToString()
            : options.ServiceVersion.Trim();

        var builder = services.AddOpenTelemetry()
            .ConfigureResource(resource => resource.AddService(
                serviceName: options.ServiceName.Trim(),
                serviceVersion: serviceVersion));

        builder.WithTracing(tracing =>
        {
            tracing
                .AddSource(AtlasTelemetry.ActivitySourceName)
                .AddSource("MassTransit")
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();

            AddConfiguredTraceExporter(tracing, options);
        });

        builder.WithMetrics(metrics =>
        {
            metrics
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation();

            if (options.InstrumentRuntime)
                metrics.AddRuntimeInstrumentation();

            AddConfiguredMetricExporter(metrics, options);
        });

        return services;
    }

    private static void AddConfiguredTraceExporter(
        TracerProviderBuilder tracing,
        AtlasOpenTelemetryOptions options)
    {
        switch (NormalizeOpenTelemetryExporter(options.Exporter))
        {
            case "console":
                tracing.AddConsoleExporter();
                break;
            case "otlp":
                tracing.AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options));
                break;
        }
    }

    private static void AddConfiguredMetricExporter(
        MeterProviderBuilder metrics,
        AtlasOpenTelemetryOptions options)
    {
        switch (NormalizeOpenTelemetryExporter(options.Exporter))
        {
            case "console":
                metrics.AddConsoleExporter();
                break;
            case "otlp":
                metrics.AddOtlpExporter(otlp => ConfigureOtlpExporter(otlp, options));
                break;
        }
    }

    private static void ConfigureOtlpExporter(
        OtlpExporterOptions otlp,
        AtlasOpenTelemetryOptions options)
    {
        otlp.Protocol = ParseOtlpProtocol(options.OtlpProtocol);
        if (Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out var endpoint))
            otlp.Endpoint = endpoint;
    }

    private static bool ValidateOpenTelemetryOptions(AtlasOpenTelemetryOptions options)
    {
        if (string.IsNullOrWhiteSpace(options.ServiceName))
            return false;

        if (!IsSupportedOpenTelemetryExporter(options.Exporter))
            return false;

        if (!IsSupportedOtlpProtocol(options.OtlpProtocol))
            return false;

        return NormalizeOpenTelemetryExporter(options.Exporter) != "otlp" ||
               Uri.TryCreate(options.OtlpEndpoint, UriKind.Absolute, out _);
    }

    private static bool IsSupportedOpenTelemetryExporter(string? exporter)
    {
        return NormalizeOpenTelemetryExporter(exporter) is "none" or "console" or "otlp";
    }

    private static string NormalizeOpenTelemetryExporter(string? exporter)
    {
        return string.IsNullOrWhiteSpace(exporter)
            ? "none"
            : exporter.Trim().ToLowerInvariant();
    }

    private static bool IsSupportedOtlpProtocol(string? protocol)
    {
        return NormalizeOtlpProtocol(protocol) is "grpc" or "http/protobuf" or "httpprotobuf" or "http";
    }

    private static OtlpExportProtocol ParseOtlpProtocol(string? protocol)
    {
        return NormalizeOtlpProtocol(protocol) switch
        {
            "http" or "http/protobuf" or "httpprotobuf" => OtlpExportProtocol.HttpProtobuf,
            _ => OtlpExportProtocol.Grpc
        };
    }

    private static string NormalizeOtlpProtocol(string? protocol)
    {
        return string.IsNullOrWhiteSpace(protocol)
            ? "grpc"
            : protocol.Trim().ToLowerInvariant();
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

    private static bool HasConfiguredValue(IConfigurationSection section, string key)
    {
        return section.GetChildren().Any(child => string.Equals(child.Key, key, StringComparison.OrdinalIgnoreCase));
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
