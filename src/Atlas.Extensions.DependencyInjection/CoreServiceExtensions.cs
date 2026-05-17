using Atlas.Core.Configuration;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Global;
using Atlas.Data.Global.Repositories;
using Atlas.Data.Global.Repositories.Impl;
using Atlas.Data.Global.UnitOfWork;
using Atlas.Data.Tenant;
using Atlas.Data.Tenant.Context;
using Atlas.Data.Tenant.Identity;
using Atlas.Data.Tenant.Providers;
using Atlas.Data.Tenant.Repositories;
using Atlas.Data.Tenant.Repositories.Impl;
using Atlas.Data.Tenant.Sql;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Locking;
using Atlas.Infrastructure.Common.Tenants;
using Atlas.Messaging.Abstractions;
using Atlas.Messaging.RabbitMQ;
using Atlas.Services;
using Atlas.Services.Abstractions;
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
        return services.AddAtlasCore(configuration, Array.Empty<Assembly>());
    }

    /// <summary>
    /// Registers all Atlas core services and optional MassTransit consumer assemblies.
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration,
        params Assembly[] messagingConsumerAssemblies)
    {
        // 注册顺序体现运行时依赖：身份、缓存和消息等基础设施先于业务服务。
        services.AddLogging();
        services.AddHttpContextAccessor();
        services.AddAtlasSnowflakeId(configuration);
        services.AddAtlasDatabase(configuration);
        services.AddAtlasIdentity();
        services.AddAtlasCache(configuration);
        services.AddAtlasMessaging(configuration, messagingConsumerAssemblies ?? Array.Empty<Assembly>());
        services.AddAtlasBusinessServices();
        services.AddAtlasBackgroundTasks(configuration);

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
        var provider = configuration["CacheSettings:Provider"]?.ToLowerInvariant()
            ?? DefaultCacheProvider;

        services.AddAtlasCaching();
        
        // 默认内存锁只适合单实例；Redis/数据库锁接入后应在这里替换为跨实例实现。
        services.TryAddSingleton<IDistributedLockProvider, MemoryDistributedLockProvider>();

        return provider switch
        {
            "redis" => services.AddAtlasRedisCache(configuration),
            "hybrid" => services.AddAtlasHybridCache(configuration),
            _ => services.AddMemoryCaching()
        };
    }

    private static IServiceCollection AddAtlasRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["CacheSettings:Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Redis connection string is required.");

        var instanceName = configuration["CacheSettings:Redis:InstanceName"];
        return services.AddRedisCaching(connectionString, instanceName);
    }

    private static IServiceCollection AddAtlasHybridCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["CacheSettings:Hybrid:RedisConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
            throw new InvalidOperationException("Hybrid cache Redis connection string is required.");

        return services.AddHybridCaching(connectionString, options =>
        {
            var l1Minutes = configuration.GetValue<int?>("CacheSettings:Hybrid:L1ExpirationMinutes");
            if (l1Minutes.HasValue)
                options.L1Expiration = TimeSpan.FromMinutes(l1Minutes.Value);
        });
    }

    #endregion

    #region Infrastructure - Messaging

    private static IServiceCollection AddAtlasMessaging(
        this IServiceCollection services,
        IConfiguration configuration,
        Assembly[] messagingConsumerAssemblies)
    {
        var provider = configuration["Messaging:Provider"]?.Trim().ToLowerInvariant()
            ?? "none";

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

        services.Configure<RabbitMqMessagingOptions>(rabbitMqSection);
        services.Configure<TenantOutboxDispatcherOptions>(configuration.GetSection("Messaging:TenantOutbox"));

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

        services.Configure<SnowflakeOptions>(opt =>
        {
            opt.WorkerId = options.WorkerId;
            opt.DatacenterId = options.DatacenterId;
        });

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

    #endregion

    #region Business Services

    /// <summary>
    /// Registers business layer services with proper dependency resolution order:
    /// 1. TenantDbConnProvider
    /// 2. TenantDbContextFactory
    /// 3. DataScope (decoupled from repositories)
    /// 4. Repositories and Unit of Work
    /// </summary>
    private static IServiceCollection AddAtlasBusinessServices(this IServiceCollection services)
    {
        // Data access foundation
        services.AddScoped<ITenantDbConnProvider, TenantDbConnProvider>();
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

        // Data scope with lazy dependencies to avoid circular references
        services.AddScoped<IDataScope>(sp =>
        {
            // DataScope 使用 Lazy 包装缓存服务，避免与仓储/租户上下文初始化形成构造期循环依赖。
            var cache = sp.GetRequiredService<ICacheService>();
            var identity = sp.GetRequiredService<ICurrentIdentity>();
            var dbFactory = sp.GetRequiredService<ITenantDbContextFactory>();
            var logger = sp.GetRequiredService<ILogger<DataScope>>();

            return new DataScope(
                new Lazy<ICacheService>(() => cache),
                identity,
                dbFactory,
                logger);
        });
        services.AddAutoMapper(_ => { }, typeof(ProductService).Assembly);

        // Global layer
        services.AddScoped<ITenantRepository,TenantRepository>();
        services.AddScoped<IGlobalUnitOfWork, GlobalUnitOfWork>();

        // Repository layer
        services.AddScoped<IUnitOfWork, TenantUnitOfWork>();
        services.AddScoped<ITenantSqlExecutor, TenantSqlExecutor>();
        services.AddScoped(typeof(IRepository<>), typeof(RepositoryBase<>));
        services.AddScoped<IStoreRepository, StoreRepository>();
        services.AddScoped<IOperationLogRepository, OperationLogRepository>();
        services.TryAddSingleton<ITenantCodeGenerator, TenantCodeGenerator>();
        services.AddScoped<ITenantConsumerRuntime, TenantConsumerRuntime>();
        services.AddScoped<ITenantDomainEventOutbox, TenantDomainEventOutbox>();
        services.AddScoped<ITenantProvisioningService, TenantProvisioningService>();
        services.AddScoped<IOrderCommandService, OrderCommandService>();
        services.AddScoped<IStoreService, StoreService>();
        services.AddScoped<IProductService, ProductService>();
        services.AddScoped<IUserService, UserService>();
        services.AddScoped<IUserLoginLogService, UserLoginLogService>();
        services.AddScoped<IOperationLogService, OperationLogService>();
        return services;
    }

    #endregion
}
