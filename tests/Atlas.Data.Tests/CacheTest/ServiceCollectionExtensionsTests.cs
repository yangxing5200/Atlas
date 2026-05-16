using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Providers.Redis;
using Atlas.Infrastructure.Caching.Tags;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Extensions
{
    public class ServiceCollectionExtensionsTests
    {
        [Fact]
        public void AddRedisCaching_ReplacesDefaultTagVersionStore()
        {
            // Arrange
            var services = new ServiceCollection();
            var redis = new Mock<IConnectionMultiplexer>();

            // Act
            services.AddAtlasCaching();
            services.AddRedisCaching(redis.Object, enableInvalidationBus: false);

            // Assert
            services.Count(x => x.ServiceType == typeof(ITagVersionStore)).Should().Be(1);
            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<ITagVersionStore>().Should().BeOfType<RedisTagVersionStore>();
        }

        [Fact]
        public void AddHybridCaching_ReplacesDefaultTagVersionStore()
        {
            // Arrange
            var services = new ServiceCollection();
            var redis = new Mock<IConnectionMultiplexer>();

            // Act
            services.AddAtlasCaching();
            services.AddHybridCaching(redis.Object, enableInvalidationBus: false);

            // Assert
            services.Count(x => x.ServiceType == typeof(ITagVersionStore)).Should().Be(1);
            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<ITagVersionStore>().Should().BeOfType<RedisTagVersionStore>();
        }

        [Fact]
        public void AddMemoryCaching_ReplacesRedisTagVersionStore()
        {
            // Arrange
            var services = new ServiceCollection();
            var redis = new Mock<IConnectionMultiplexer>();

            // Act
            services.AddAtlasCaching();
            services.AddRedisCaching(redis.Object, enableInvalidationBus: false);
            services.AddMemoryCaching();

            // Assert
            services.Count(x => x.ServiceType == typeof(ITagVersionStore)).Should().Be(1);
            using var provider = services.BuildServiceProvider();
            provider.GetRequiredService<ITagVersionStore>().Should().BeOfType<TagVersionStore>();
        }
    }
}
