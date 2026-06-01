using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Invalidation;
using Atlas.Infrastructure.Caching.Keys.Generators;
using Atlas.Infrastructure.Caching.Keys.Parsers;
using Atlas.Infrastructure.Caching.Locking;
using Atlas.Infrastructure.Caching.Providers.Memory;
using Atlas.Infrastructure.Caching.Providers.Redis;
using Atlas.Infrastructure.Caching.Providers.Hybrid;
using Atlas.Infrastructure.Caching.Scoping;
using Atlas.Infrastructure.Caching.Serialization;
using Atlas.Infrastructure.Caching.Tags;

namespace Atlas.Infrastructure.Caching.Extensions
{
    /// <summary>
    /// Atlas 缓存模块的依赖注入入口。
    /// </summary>
    /// <remarks>
    /// AddAtlasCaching 注册抽象和默认组件；AddMemory/AddRedis/AddHybrid 负责选择具体 Provider。
    /// </remarks>
    public static class ServiceCollectionExtensions
    {
        /// <summary>
        /// Adds the core Atlas caching services.
        /// </summary>
        public static IServiceCollection AddAtlasCaching(
            this IServiceCollection services,
            Action<CachingOptionsBuilder>? configure = null)
        {
            var builder = new CachingOptionsBuilder(services);
            configure?.Invoke(builder);

            // Core services. TryAdd 允许宿主在调用前预先替换序列化、键生成或作用域解析策略。
            services.TryAddSingleton<ICacheSerializer>(sp => new JsonCacheSerializer());
            services.TryAddSingleton<ICacheKeyGenerator>(sp => new CacheKeyGenerator());
            services.TryAddSingleton<ICacheKeyParser>(sp => new CacheKeyParser());
            services.TryAddScoped<IScopeContextAccessor, CurrentUserScopeContextAccessor>();

            // Tag version storage. Concrete providers can replace the default in-memory store.
            services.TryAddSingleton<ITagVersionStore, TagVersionStore>();
            services.TryAddSingleton<ITagManager, TagManager>();

            // Cache invalidation. 分布式通知总线由具体 Provider 注册。
            services.TryAddSingleton<ICacheInvalidator, CacheInvalidator>();

            // Main cache service.
            services.TryAddScoped<ICacheService, CacheService>();

            return services;
        }

        /// <summary>
        /// Adds in-memory caching. This mode does not support cross-instance invalidation.
        /// </summary>
        public static IServiceCollection AddMemoryCaching(this IServiceCollection services)
        {
            services.AddMemoryCache();
            // 切换 Provider 前移除旧注册，确保容器中只有一个 ICacheProvider 生效。
            services.RemoveAll<ICacheProvider>();
            services.AddSingleton<ICacheProvider, MemoryCacheProvider>();

            services.RemoveAll<ITagVersionStore>();
            services.AddSingleton<ITagVersionStore, TagVersionStore>();
            // 单机内存缓存没有跨实例通知能力，显式移除失效总线。
            services.RemoveAll<ICacheInvalidationBus>();
            services.RemoveAll<IDistributedLockProvider>();
            services.AddSingleton<IDistributedLockProvider, MemoryDistributedLockProvider>();
            return services;
        }

        /// <summary>
        /// Adds Redis-backed caching with shared cache data and invalidation notifications.
        /// </summary>
        public static IServiceCollection AddRedisCaching(
            this IServiceCollection services,
            string connectionString,
            string? instanceName = null,
            bool enableInvalidationBus = true)
        {
            var redis = ConnectionMultiplexer.Connect(connectionString);
            return services.AddRedisCaching(redis, instanceName, enableInvalidationBus);
        }

        /// <summary>
        /// Adds Redis-backed caching by using an existing connection.
        /// </summary>
        public static IServiceCollection AddRedisCaching(
            this IServiceCollection services,
            IConnectionMultiplexer redis,
            string? instanceName = null,
            bool enableInvalidationBus = true)
        {
            ArgumentNullException.ThrowIfNull(redis);

            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(redis);
            services.RemoveAll<IDistributedLockProvider>();
            services.AddSingleton<IDistributedLockProvider>(sp =>
                new RedisDistributedLockProvider(redis, instanceName ?? "atlas"));

            services.RemoveAll<ICacheProvider>();
            services.AddSingleton<ICacheProvider>(sp =>
                new RedisCacheProvider(redis, instanceName ?? "atlas"));

            services.RemoveAll<ITagVersionStore>();
            services.AddSingleton<ITagVersionStore>(sp =>
                new RedisTagVersionStore(redis));

            services.RemoveAll<ICacheInvalidationBus>();
            if (enableInvalidationBus)
            {
                // Redis Pub/Sub 用于通知其他实例清理本地 L1 或内存副本。
                services.AddSingleton<ICacheInvalidationBus>(sp =>
                {
                    var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<RedisInvalidationBus>>();
                    return new RedisInvalidationBus(redis, logger);
                });
            }

            return services;
        }

        /// <summary>
        /// Adds hybrid caching with local memory as L1 and Redis as L2.
        /// </summary>
        public static IServiceCollection AddHybridCaching(
            this IServiceCollection services,
            string redisConnectionString,
            Action<HybridCacheOptions>? configureOptions = null,
            bool enableInvalidationBus = true)
        {
            services.AddMemoryCache();
            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            return services.AddHybridCaching(redis, configureOptions, enableInvalidationBus);
        }

        /// <summary>
        /// Adds hybrid caching by using an existing Redis connection.
        /// </summary>
        public static IServiceCollection AddHybridCaching(
            this IServiceCollection services,
            IConnectionMultiplexer redis,
            Action<HybridCacheOptions>? configureOptions = null,
            bool enableInvalidationBus = true)
        {
            ArgumentNullException.ThrowIfNull(redis);

            services.AddMemoryCache();
            services.RemoveAll<IConnectionMultiplexer>();
            services.AddSingleton(redis);
            services.RemoveAll<IDistributedLockProvider>();
            services.AddSingleton<IDistributedLockProvider>(sp =>
                new RedisDistributedLockProvider(redis));

            var options = new HybridCacheOptions();
            configureOptions?.Invoke(options);

            services.RemoveAll<ICacheInvalidationBus>();
            if (enableInvalidationBus)
            {
                services.AddSingleton<ICacheInvalidationBus>(sp =>
                {
                    var logger = sp.GetService<Microsoft.Extensions.Logging.ILogger<RedisInvalidationBus>>();
                    return new RedisInvalidationBus(redis, logger);
                });
            }

            services.RemoveAll<ICacheProvider>();
            services.AddSingleton<ICacheProvider>(sp =>
            {
                // Hybrid Provider 将内存作为 L1、Redis 作为 L2，兼顾读性能和跨实例共享。
                var memoryCache = sp.GetRequiredService<IMemoryCache>();
                var l1 = new MemoryCacheProvider(memoryCache);
                var l2 = new RedisCacheProvider(redis, "atlas");
                var invalidationBus = sp.GetService<ICacheInvalidationBus>();
                return new HybridCacheProvider(l1, l2, options, invalidationBus);
            });

            services.RemoveAll<ITagVersionStore>();
            services.AddSingleton<ITagVersionStore>(sp =>
                new RedisTagVersionStore(redis));

            return services;
        }
    }

    public class CachingOptionsBuilder
    {
        public IServiceCollection Services { get; }

        public CachingOptionsBuilder(IServiceCollection services)
        {
            Services = services;
        }
    }
}
