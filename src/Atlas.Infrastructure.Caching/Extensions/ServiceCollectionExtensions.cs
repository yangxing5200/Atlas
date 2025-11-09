using Atlas.Infrastructure.Caching.Adapters;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Dependencies;
using Atlas.Infrastructure.Caching.Invalidation;
using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Loading;
using Atlas.Infrastructure.Caching.Metrics;
using Atlas.Infrastructure.Caching.Serialization;
using Atlas.Infrastructure.Caching.Storage;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using StackExchange.Redis;

namespace Atlas.Infrastructure.Caching.Extensions;

public static class ServiceCollectionExtensions
{
    /// <summary>
    /// 注册Atlas缓存系统及其相关服务
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">缓存配置委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection AddAtlasCache(
        this IServiceCollection services,
        Action<CacheOptions> configure)
    {
        // 应用缓存配置
        services.Configure(configure);
        var options = new CacheOptions();
        configure(options);

        // 注册身份标识适配器
        services.AddScoped<ICurrentIdentity, CurrentIdentityAdapter>();

        // 注册内存缓存提供程序
        services.AddMemoryCache(memOptions =>
        {
            memOptions.SizeLimit = options.L1CacheSizeLimitMB * 1024 * 1024;
        });

        // 注册内存存储适配器
        services.AddSingleton<MemoryStorageAdapter>();

        // 根据Redis配置状态决定存储策略
        var hasRedis = !string.IsNullOrEmpty(options.RedisConnectionString);

        if (hasRedis)
        {
            // Redis分布式缓存模式

            // 注册Redis连接复用器
            services.AddSingleton<IConnectionMultiplexer>(sp =>
                ConnectionMultiplexer.Connect(options.RedisConnectionString!));

            // 注册序列化器
            if (options.SerializerType == "MessagePack")
            {
                services.AddSingleton<ICacheSerializer, MessagePackCacheSerializer>();
            }
            else
            {
                services.AddSingleton<ICacheSerializer, JsonCacheSerializer>();
            }

            // 注册Redis存储适配器
            services.AddSingleton<RedisStorageAdapter>();

            // 注册混合存储适配器（L1内存 + L2Redis）
            services.AddSingleton<HybridStorageAdapter>();
            services.AddSingleton<IStorageAdapter>(sp => sp.GetRequiredService<HybridStorageAdapter>());

            // 注册Redis消息代理
            services.AddSingleton<IMessageBroker, RedisMessageBroker>();
        }
        else
        {
            // 本地内存缓存模式

            // 使用内存存储适配器
            services.AddSingleton<IStorageAdapter>(sp => sp.GetRequiredService<MemoryStorageAdapter>());

            // 注册空消息代理
            services.AddSingleton<IMessageBroker, NullMessageBroker>();
        }

        // 注册核心组件

        // 缓存键管理
        services.AddSingleton<CacheKeyRegistry>();
        services.AddSingleton<ICacheKeyBuilder, CacheKeyBuilder>();

        // 依赖关系管理
        services.AddSingleton<DependencyRegistry>();
        services.AddSingleton<IDependencyResolver, DependencyResolver>();

        // 缓存失效协调器
        services.AddSingleton<InvalidationCoordinator>();
        services.AddSingleton<IInvalidationCoordinator>(sp => sp.GetRequiredService<InvalidationCoordinator>());

        // 缓存加载协调器
        services.AddSingleton<LoadingCoordinator>();

        // 性能指标收集器
        services.AddSingleton<MetricsCollector>();

        // 缓存服务（多接口注册）
        services.AddSingleton<CacheService>();
        services.AddSingleton<ICacheService>(sp => sp.GetRequiredService<CacheService>());
        services.AddSingleton<IAsyncCacheService>(sp => sp.GetRequiredService<CacheService>());
        services.AddSingleton<ISyncCacheService>(sp => sp.GetRequiredService<CacheService>());

        return services;
    }

    /// <summary>
    /// 注册缓存键定义到注册表
    /// </summary>
    /// <param name="services">服务集合</param>
    /// <param name="configure">缓存键注册委托</param>
    /// <returns>服务集合</returns>
    public static IServiceCollection RegisterCacheKeys(
        this IServiceCollection services,
        Action<CacheKeyRegistry> configure)
    {
        services.AddSingleton(sp =>
        {
            var registry = sp.GetRequiredService<CacheKeyRegistry>();
            configure(registry);
            return registry;
        });

        return services;
    }
}