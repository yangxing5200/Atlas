using Atlas.Core.Configuration;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Common.Interceptors;
using Atlas.Data.Global;
using Atlas.Data.Tenant;
using Atlas.Data.Tenant.Impl;
using Atlas.Data.Tenant.Repositories;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Extensions.DependencyInjection;

/// <summary>
/// Atlas 核心服务注册扩展
/// </summary>
public static class AtlasCoreServiceExtensions
{
    /// <summary>
    /// 注册 Atlas 核心服务（包含基础设施和业务服务）
    /// </summary>
    public static IServiceCollection AddAtlasCore(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 基础设施服务
        services.AddHttpContextAccessor();
        services.AddAtlasSnowflakeId(configuration);
        services.AddAtlasDatabase(configuration);
        services.AddAtlasIdentity();
        services.AddAtlasCache(configuration);

        // 业务服务
        services.AddAtlasBusinessServices();

        return services;
    }

    #region Infrastructure - HTTP Context

    /// <summary>
    /// 注册 HTTP 上下文访问器
    /// </summary>
    private static IServiceCollection AddHttpContextAccessor(this IServiceCollection services)
    {
        services.TryAddSingleton<IHttpContextAccessor, HttpContextAccessor>();
        return services;
    }

    #endregion

    #region Infrastructure - Database

    /// <summary>
    /// 注册 Atlas 数据库服务（全局库和租户库）
    /// </summary>
    private static IServiceCollection AddAtlasDatabase(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        // 审计拦截器
        services.AddScoped<AuditInterceptor>();
        // 全局数据库
        var globalConnStr = configuration.GetConnectionString("AtlasGlobal")
            ?? throw new InvalidOperationException("AtlasGlobal connection string is required");

        services.AddDbContext<AtlasGlobalDbContext>((sp, options) =>
        {
            var auditInterceptor = sp.GetRequiredService<AuditInterceptor>();
            options.UseMySql(globalConnStr, ServerVersion.AutoDetect(globalConnStr))
                   .AddInterceptors(auditInterceptor);
        });

        // 租户数据库
        services.AddScoped<ITenantDbConnProvider, TenantDbConnProvider>();
        services.AddScoped<ITenantDbContextFactory, TenantDbContextFactory>();

        return services;
    }

    #endregion

    #region Infrastructure - Identity

    /// <summary>
    /// 注册当前身份服务
    /// </summary>
    /// <remarks>使用 Lazy 注入 StoreRepository 避免循环依赖</remarks>
    private static IServiceCollection AddAtlasIdentity(this IServiceCollection services)
    {
        services.AddScoped<ICurrentIdentity>(sp =>
        {
            var httpContextAccessor = sp.GetRequiredService<IHttpContextAccessor>();
            var cache = sp.GetRequiredService<ICacheService>();
            var lazyStoreRepository = new Lazy<IStoreRepository>(() =>
                sp.GetRequiredService<IStoreRepository>());

            return new CurrentIdentity(httpContextAccessor, lazyStoreRepository, cache);
        });

        return services;
    }

    #endregion

    #region Infrastructure - Cache

    /// <summary>
    /// 注册 Atlas 缓存服务
    /// </summary>
    /// <remarks>
    /// 支持三种缓存模式：
    /// - Memory: 内存缓存（默认）
    /// - Redis: Redis 分布式缓存
    /// - Hybrid: 混合缓存（L1 内存 + L2 Redis）
    /// </remarks>
    private static IServiceCollection AddAtlasCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var cacheProvider = configuration["CacheSettings:Provider"]?.ToLowerInvariant() ?? "memory";

        services.AddAtlasCaching();

        switch (cacheProvider)
        {
            case "redis":
                services.AddAtlasRedisCache(configuration);
                break;

            case "hybrid":
                services.AddAtlasHybridCache(configuration);
                break;

            case "memory":
            default:
                services.AddMemoryCaching();
                break;
        }

        return services;
    }

    /// <summary>
    /// 配置 Redis 缓存
    /// </summary>
    private static IServiceCollection AddAtlasRedisCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["CacheSettings:Redis:ConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CacheSettings:Redis:ConnectionString is required when using Redis cache");
        }

        var instanceName = configuration["CacheSettings:Redis:InstanceName"];
        services.AddRedisCaching(connectionString, instanceName);

        return services;
    }

    /// <summary>
    /// 配置混合缓存（L1 内存 + L2 Redis）
    /// </summary>
    private static IServiceCollection AddAtlasHybridCache(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = configuration["CacheSettings:Hybrid:RedisConnectionString"];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "CacheSettings:Hybrid:RedisConnectionString is required when using Hybrid cache");
        }

        services.AddHybridCaching(connectionString, options =>
        {
            var l1ExpirationMinutes = configuration.GetValue<int?>(
                "CacheSettings:Hybrid:L1ExpirationMinutes");

            if (l1ExpirationMinutes.HasValue)
            {
                options.L1Expiration = TimeSpan.FromMinutes(l1ExpirationMinutes.Value);
            }
        });

        return services;
    }

    #endregion

    #region Infrastructure - ID Generator

    /// <summary>
    /// 从配置文件注册 Snowflake ID 生成器
    /// </summary>
    /// <remarks>
    /// appsettings.json 配置示例：
    /// <code>
    /// {
    ///   "Snowflake": {
    ///     "WorkerId": 1,
    ///     "DatacenterId": 1
    ///   }
    /// }
    /// </code>
    /// </remarks>
    public static IServiceCollection AddAtlasSnowflakeId(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var section = configuration.GetSection("Snowflake");
        var options = section.Get<SnowflakeOptions>() ?? GetDefaultSnowflakeOptions();

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
    /// 手动指定参数注册 Snowflake ID 生成器
    /// </summary>
    /// <param name="workerId">机器ID (0-31)</param>
    /// <param name="datacenterId">数据中心ID (0-31)</param>
    public static IServiceCollection AddAtlasSnowflakeId(
        this IServiceCollection services,
        long workerId,
        long datacenterId)
    {
        var options = new SnowflakeOptions
        {
            WorkerId = workerId,
            DatacenterId = datacenterId
        };
        options.Validate();

        services.TryAddSingleton<IIdGenerator>(
            new SnowflakeIdGenerator(workerId, datacenterId));

        return services;
    }

    /// <summary>
    /// 自动检测 WorkerId 注册 Snowflake ID 生成器
    /// </summary>
    /// <param name="datacenterId">数据中心ID</param>
    /// <remarks>WorkerId 将基于环境变量或机器名自动生成 (0-31)</remarks>
    public static IServiceCollection AddAtlasSnowflakeIdAuto(
        this IServiceCollection services,
        long datacenterId = 1)
    {
        var workerId = GetAutoWorkerId();
        return services.AddAtlasSnowflakeId(workerId, datacenterId);
    }

    /// <summary>
    /// 从委托工厂注册 Snowflake ID 生成器
    /// </summary>
    /// <remarks>适用于运行时动态确定配置的场景</remarks>
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
    /// 获取默认 Snowflake 配置
    /// </summary>
    /// <remarks>
    /// 优先级：
    /// 1. 环境变量 SNOWFLAKE_WORKER_ID 和 SNOWFLAKE_DATACENTER_ID
    /// 2. 自动生成（基于机器名 Hash）
    /// </remarks>
    private static SnowflakeOptions GetDefaultSnowflakeOptions()
    {
        var envWorkerId = Environment.GetEnvironmentVariable("SNOWFLAKE_WORKER_ID");
        var envDatacenterId = Environment.GetEnvironmentVariable("SNOWFLAKE_DATACENTER_ID");

        if (!string.IsNullOrEmpty(envWorkerId) && long.TryParse(envWorkerId, out var workerId) &&
            !string.IsNullOrEmpty(envDatacenterId) && long.TryParse(envDatacenterId, out var datacenterId))
        {
            return new SnowflakeOptions
            {
                WorkerId = workerId,
                DatacenterId = datacenterId
            };
        }

        return new SnowflakeOptions
        {
            WorkerId = GetAutoWorkerId(),
            DatacenterId = 1
        };
    }

    /// <summary>
    /// 自动获取 WorkerId
    /// </summary>
    /// <remarks>
    /// 优先级：
    /// 1. 环境变量 SNOWFLAKE_WORKER_ID
    /// 2. 机器名 Hash 取模 (0-31)
    /// </remarks>
    private static long GetAutoWorkerId()
    {
        var envWorkerId = Environment.GetEnvironmentVariable("SNOWFLAKE_WORKER_ID");
        if (!string.IsNullOrEmpty(envWorkerId) && long.TryParse(envWorkerId, out var parsedId))
        {
            if (parsedId is >= 0 and <= 31)
                return parsedId;
        }

        var machineName = Environment.MachineName;
        var hash = machineName.GetHashCode();
        return Math.Abs(hash) % 32;
    }

    #endregion

    #region Business Services

    /// <summary>
    /// 注册业务服务（包含仓储和服务层）
    /// </summary>
    private static IServiceCollection AddAtlasBusinessServices(this IServiceCollection services)
    {
        // ========== 仓储层 ==========
        services.AddScoped<IStoreRepository, StoreRepository>();
        // services.AddScoped<IProductRepository, ProductRepository>();
        // services.AddScoped<IOrderRepository, OrderRepository>();
        // services.AddScoped<IInventoryRepository, InventoryRepository>();

        // ========== 业务服务层 ==========
        // services.AddScoped<IStoreService, StoreService>();
        // services.AddScoped<IProductService, ProductService>();
        // services.AddScoped<IOrderService, OrderService>();
        // services.AddScoped<IInventoryService, InventoryService>();

        return services;
    }

    #endregion
}