// Extensions/ServiceCollectionExtensions.cs
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using StackExchange.Redis;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Invalidation;
using Atlas.Infrastructure.Caching.Keys.Generators;
using Atlas.Infrastructure.Caching.Keys.Parsers;
using Atlas.Infrastructure.Caching.Providers.Memory;
using Atlas.Infrastructure.Caching.Providers.Redis;
using Atlas.Infrastructure.Caching.Providers.Hybrid;
using Atlas.Infrastructure.Caching.Scoping;
using Atlas.Infrastructure.Caching.Serialization;
using Atlas.Infrastructure.Caching.Tags;

namespace Atlas.Infrastructure.Caching.Extensions
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddAtlasCaching(
            this IServiceCollection services,
            Action<CachingOptionsBuilder>? configure = null)
        {
            var builder = new CachingOptionsBuilder(services);
            configure?.Invoke(builder);

            // Core services - 使用具体类型注册
            services.TryAddSingleton<ICacheSerializer>(sp => new JsonCacheSerializer());
            services.TryAddSingleton<ICacheKeyGenerator>(sp => new CacheKeyGenerator());
            services.TryAddSingleton<ICacheKeyParser>(sp => new CacheKeyParser());
            services.TryAddSingleton<IScopeContextAccessor, CurrentUserScopeContextAccessor>();

            // Tag management
            services.TryAddSingleton<ITagVersionStore, TagVersionStore>();
            services.TryAddSingleton<ITagManager, TagManager>();

            // Invalidation
            services.TryAddSingleton<ICacheInvalidator, CacheInvalidator>();

            // Main cache service
            services.TryAddSingleton<ICacheService, CacheService>();
            return services;
        }

        public static IServiceCollection AddMemoryCaching(this IServiceCollection services)
        {
            services.AddMemoryCache();
            services.TryAddSingleton<ICacheProvider, MemoryCacheProvider>();
            return services;
        }

        public static IServiceCollection AddRedisCaching(
            this IServiceCollection services,
            string connectionString,
            string? instanceName = null)
        {
            var redis = ConnectionMultiplexer.Connect(connectionString);
            services.AddSingleton<IConnectionMultiplexer>(redis);
            services.TryAddSingleton<ICacheProvider>(sp =>
                new RedisCacheProvider(redis, instanceName ?? "atlas"));
            services.TryAddSingleton<ITagVersionStore>(sp =>
                new RedisTagVersionStore(redis));
            return services;
        }

        public static IServiceCollection AddHybridCaching(
            this IServiceCollection services,
            string redisConnectionString,
            Action<HybridCacheOptions>? configureOptions = null)
        {
            services.AddMemoryCache();
            var redis = ConnectionMultiplexer.Connect(redisConnectionString);
            services.AddSingleton<IConnectionMultiplexer>(redis);

            var options = new HybridCacheOptions();
            configureOptions?.Invoke(options);

            services.TryAddSingleton<ICacheProvider>(sp =>
            {
                var memoryCache = sp.GetRequiredService<IMemoryCache>();
                var l1 = new MemoryCacheProvider(memoryCache);
                var l2 = new RedisCacheProvider(redis, "atlas");
                return new HybridCacheProvider(l1, l2, options);
            });

            services.TryAddSingleton<ITagVersionStore>(sp => new RedisTagVersionStore(redis));

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