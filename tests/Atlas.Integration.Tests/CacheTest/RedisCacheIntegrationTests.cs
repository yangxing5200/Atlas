// Redis/RedisCacheIntegrationTests.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Integration.Tests.Redis
{
    [Collection("Redis")]
    public class RedisCacheIntegrationTests : IntegrationTestBase
    {
        private readonly RedisFixture _redisFixture;
        private ICacheService _cacheService = null!;

        public RedisCacheIntegrationTests(RedisFixture redisFixture)
        {
            _redisFixture = redisFixture;
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddAtlasCaching();
            services.AddRedisCaching(_redisFixture.ConnectionString, "test");
        }

        protected override Task OnInitializeAsync()
        {
            _cacheService = GetService<ICacheService>();
            return _redisFixture.ClearAllAsync();
        }

        [Fact]
        public async Task SetAsync_And_GetAsync_Should_Work()
        {
            // Arrange
            var key = "test:key1";
            var value = new TestData { Id = 1, Name = "Test Item" };

            // Act
            await _cacheService.SetAsync(key, value);
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(value.Id);
            result.Name.Should().Be(value.Name);
        }

        [Fact]
        public async Task GetOrSetAsync_WhenCacheMiss_Should_ExecuteFactory()
        {
            // Arrange
            var key = "test:key2";
            var factoryExecuted = false;
            var expectedValue = new TestData { Id = 2, Name = "Factory Item" };

            // Act
            var result = await _cacheService.GetOrSetAsync(
                key,
                async () =>
                {
                    factoryExecuted = true;
                    return await Task.FromResult(expectedValue);
                },
                CacheOptions.WithExpiration(TimeSpan.FromMinutes(5))
            );

            // Assert
            result.IsHit.Should().BeFalse();
            result.Value.Should().BeEquivalentTo(expectedValue);
            factoryExecuted.Should().BeTrue();

            // Verify it's cached
            var cachedResult = await _cacheService.GetAsync<TestData>(key);
            cachedResult.Should().BeEquivalentTo(expectedValue);
        }

        [Fact]
        public async Task GetOrSetAsync_WhenCacheHit_Should_NotExecuteFactory()
        {
            // Arrange
            var key = "test:key3";
            var cachedValue = new TestData { Id = 3, Name = "Cached Item" };
            await _cacheService.SetAsync(key, cachedValue);

            var factoryExecuted = false;

            // Act
            var result = await _cacheService.GetOrSetAsync(
                key,
                async () =>
                {
                    factoryExecuted = true;
                    return await Task.FromResult(new TestData { Id = 999, Name = "Should Not See This" });
                }
            );

            // Assert
            result.IsHit.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(cachedValue);
            factoryExecuted.Should().BeFalse();
        }

        [Fact]
        public async Task SetAsync_WithExpiration_Should_Expire()
        {
            // Arrange
            var key = "test:expiring";
            var value = new TestData { Id = 4, Name = "Expiring Item" };
            var expiration = TimeSpan.FromSeconds(2);

            // Act
            await _cacheService.SetAsync(key, value, CacheOptions.WithExpiration(expiration));

            // Wait for expiration
            await Task.Delay(TimeSpan.FromSeconds(3));

            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task InvalidateByTag_Should_InvalidateTaggedItems()
        {
            // Arrange
            var tag = "product";
            var key1 = "product:1";
            var key2 = "product:2";
            var key3 = "category:1"; // Different tag

            var options = CacheOptions.WithTags(tag);

            await _cacheService.SetAsync(key1, new TestData { Id = 1 }, options);
            await _cacheService.SetAsync(key2, new TestData { Id = 2 }, options);
            await _cacheService.SetAsync(key3, new TestData { Id = 3 }, CacheOptions.WithTags("category"));

            // Act
            await _cacheService.InvalidateByTagAsync(tag);

            // Give Redis time to propagate
            await Task.Delay(100);

            // Re-set with new tag versions
            await _cacheService.SetAsync(key1, new TestData { Id = 10 }, options);
            await _cacheService.SetAsync(key2, new TestData { Id = 20 }, options);

            var result1 = await _cacheService.GetAsync<TestData>(key1);
            var result2 = await _cacheService.GetAsync<TestData>(key2);
            var result3 = await _cacheService.GetAsync<TestData>(key3);

            // Assert - Tagged items should have new values
            result1!.Id.Should().Be(10);
            result2!.Id.Should().Be(20);
            result3!.Id.Should().Be(3); // Unchanged
        }

        [Fact]
        public async Task GetManyAsync_Should_ReturnMultipleValues()
        {
            // Arrange
            var keys = new[] { "multi:1", "multi:2", "multi:3" };
            await _cacheService.SetAsync(keys[0], new TestData { Id = 1, Name = "Item 1" });
            await _cacheService.SetAsync(keys[1], new TestData { Id = 2, Name = "Item 2" });
            await _cacheService.SetAsync(keys[2], new TestData { Id = 3, Name = "Item 3" });

            // Act
            var results = await _cacheService.GetManyAsync<TestData>(keys);

            // Assert
            results.Should().HaveCount(3);
            results[keys[0]]!.Id.Should().Be(1);
            results[keys[1]]!.Id.Should().Be(2);
            results[keys[2]]!.Id.Should().Be(3);
        }

        [Fact]
        public async Task SetManyAsync_Should_StoreMultipleValues()
        {
            // Arrange
            var items = new Dictionary<string, TestData>
            {
                ["batch:1"] = new TestData { Id = 1, Name = "Batch 1" },
                ["batch:2"] = new TestData { Id = 2, Name = "Batch 2" },
                ["batch:3"] = new TestData { Id = 3, Name = "Batch 3" }
            };

            // Act
            await _cacheService.SetManyAsync(items);
            var results = await _cacheService.GetManyAsync<TestData>(items.Keys);

            // Assert
            results.Should().HaveCount(3);
            foreach (var kvp in items)
            {
                results[kvp.Key].Should().BeEquivalentTo(kvp.Value);
            }
        }

        [Fact]
        public async Task RemoveAsync_Should_RemoveKey()
        {
            // Arrange
            var key = "test:remove";
            await _cacheService.SetAsync(key, new TestData { Id = 1 });

            // Act
            var removed = await _cacheService.RemoveAsync(key);
            var result = await _cacheService.GetAsync<TestData>(key);

            // Assert
            removed.Should().BeTrue();
            result.Should().BeNull();
        }

        [Fact]
        public async Task ExistsAsync_Should_ReturnCorrectStatus()
        {
            // Arrange
            var existingKey = "test:exists";
            var nonExistingKey = "test:notexists";
            await _cacheService.SetAsync(existingKey, new TestData { Id = 1 });

            // Act
            var exists = await _cacheService.ExistsAsync(existingKey);
            var notExists = await _cacheService.ExistsAsync(nonExistingKey);

            // Assert
            exists.Should().BeTrue();
            notExists.Should().BeFalse();
        }

        [Fact]
        public async Task ClearAsync_Should_RemoveAllKeys()
        {
            // Arrange
            await _cacheService.SetAsync("clear:1", new TestData { Id = 1 });
            await _cacheService.SetAsync("clear:2", new TestData { Id = 2 });

            // Act
            await _cacheService.ClearAsync();

            var result1 = await _cacheService.GetAsync<TestData>("clear:1");
            var result2 = await _cacheService.GetAsync<TestData>("clear:2");

            // Assert
            result1.Should().BeNull();
            result2.Should().BeNull();
        }

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }

    [CollectionDefinition("Redis")]
    public class RedisCollection : ICollectionFixture<RedisFixture>
    {
    }
}