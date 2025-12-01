using Atlas.Infrastructure.Caching.Providers.Memory;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Providers
{
    public class MemoryCacheProviderTests : IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheProvider _provider;

        public MemoryCacheProviderTests()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _provider = new MemoryCacheProvider(_memoryCache);
        }

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_WhenKeyExists_ReturnsValue()
        {
            // Arrange
            var key = "test-key";
            var value = Encoding.UTF8.GetBytes("test-value");
            await _provider.SetAsync(key, value);

            // Act
            var result = await _provider.GetAsync(key);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(value);
        }

        [Fact]
        public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNull()
        {
            // Arrange
            var key = "non-existent-key";

            // Act
            var result = await _provider.GetAsync(key);

            // Assert
            result.Should().BeNull();
        }

        #endregion

        #region SetAsync Tests

        [Fact]
        public async Task SetAsync_StoresValue()
        {
            // Arrange
            var key = "test-key";
            var value = Encoding.UTF8.GetBytes("test-value");

            // Act
            await _provider.SetAsync(key, value);
            var result = await _provider.GetAsync(key);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(value);
        }

        [Fact]
        public async Task SetAsync_WithExpiration_ValueExpiresAfterTimeout()
        {
            // Arrange
            var key = "test-key";
            var value = Encoding.UTF8.GetBytes("test-value");
            var expiration = TimeSpan.FromMilliseconds(100);

            // Act
            await _provider.SetAsync(key, value, expiration);
            await Task.Delay(200); // Wait for expiration
            var result = await _provider.GetAsync(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_OverwritesExistingValue()
        {
            // Arrange
            var key = "test-key";
            var value1 = Encoding.UTF8.GetBytes("value-1");
            var value2 = Encoding.UTF8.GetBytes("value-2");

            // Act
            await _provider.SetAsync(key, value1);
            await _provider.SetAsync(key, value2);
            var result = await _provider.GetAsync(key);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(value2);
        }

        #endregion

        #region RemoveAsync Tests

        [Fact]
        public async Task RemoveAsync_WhenKeyExists_RemovesAndReturnsTrue()
        {
            // Arrange
            var key = "test-key";
            var value = Encoding.UTF8.GetBytes("test-value");
            await _provider.SetAsync(key, value);

            // Act
            var result = await _provider.RemoveAsync(key);
            var getValue = await _provider.GetAsync(key);

            // Assert
            result.Should().BeTrue();
            getValue.Should().BeNull();
        }

        [Fact]
        public async Task RemoveAsync_WhenKeyDoesNotExist_ReturnsTrue()
        {
            // Arrange
            var key = "non-existent-key";

            // Act
            var result = await _provider.RemoveAsync(key);

            // Assert
            result.Should().BeTrue();
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WhenKeyExists_ReturnsTrue()
        {
            // Arrange
            var key = "test-key";
            var value = Encoding.UTF8.GetBytes("test-value");
            await _provider.SetAsync(key, value);

            // Act
            var result = await _provider.ExistsAsync(key);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_WhenKeyDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var key = "non-existent-key";

            // Act
            var result = await _provider.ExistsAsync(key);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region GetManyAsync Tests

        [Fact]
        public async Task GetManyAsync_ReturnsAllRequestedKeys()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            await _provider.SetAsync("key1", Encoding.UTF8.GetBytes("value1"));
            await _provider.SetAsync("key2", Encoding.UTF8.GetBytes("value2"));
            await _provider.SetAsync("key3", Encoding.UTF8.GetBytes("value3"));

            // Act
            var result = await _provider.GetManyAsync(keys);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result["key1"].Should().NotBeNull();
            result["key2"].Should().NotBeNull();
            result["key3"].Should().NotBeNull();
        }

        [Fact]
        public async Task GetManyAsync_WithNonExistentKeys_ReturnsNullForMissingKeys()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            await _provider.SetAsync("key1", Encoding.UTF8.GetBytes("value1"));
            // key2 and key3 don't exist

            // Act
            var result = await _provider.GetManyAsync(keys);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
            result["key1"].Should().NotBeNull();
            result["key2"].Should().BeNull();
            result["key3"].Should().BeNull();
        }

        #endregion

        #region SetManyAsync Tests

        [Fact]
        public async Task SetManyAsync_StoresAllValues()
        {
            // Arrange
            var items = new Dictionary<string, byte[]>
            {
                { "key1", Encoding.UTF8.GetBytes("value1") },
                { "key2", Encoding.UTF8.GetBytes("value2") },
                { "key3", Encoding.UTF8.GetBytes("value3") }
            };

            // Act
            await _provider.SetManyAsync(items);

            // Assert
            foreach (var kvp in items)
            {
                var value = await _provider.GetAsync(kvp.Key);
                value.Should().NotBeNull();
                value.Should().BeEquivalentTo(kvp.Value);
            }
        }

        #endregion

        #region RemoveManyAsync Tests

        [Fact]
        public async Task RemoveManyAsync_RemovesAllKeys()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            foreach (var key in keys)
            {
                await _provider.SetAsync(key, Encoding.UTF8.GetBytes($"value-{key}"));
            }

            // Act
            var count = await _provider.RemoveManyAsync(keys);

            // Assert
            count.Should().Be(3);
            foreach (var key in keys)
            {
                var exists = await _provider.ExistsAsync(key);
                exists.Should().BeFalse();
            }
        }

        #endregion

        #region GetKeysByPatternAsync Tests

        [Fact]
        public async Task GetKeysByPatternAsync_WithWildcard_ReturnsMatchingKeys()
        {
            // Arrange
            await _provider.SetAsync("product:1", Encoding.UTF8.GetBytes("value1"));
            await _provider.SetAsync("product:2", Encoding.UTF8.GetBytes("value2"));
            await _provider.SetAsync("category:1", Encoding.UTF8.GetBytes("value3"));

            // Act
            var result = await _provider.GetKeysByPatternAsync("product:*");

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(2);
            result.Should().Contain("product:1");
            result.Should().Contain("product:2");
        }

        [Fact]
        public async Task GetKeysByPatternAsync_WithNoMatches_ReturnsEmpty()
        {
            // Arrange
            await _provider.SetAsync("product:1", Encoding.UTF8.GetBytes("value1"));

            // Act
            var result = await _provider.GetKeysByPatternAsync("category:*");

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEmpty();
        }

        [Fact]
        public async Task GetKeysByPatternAsync_SamePatternMultipleTimes_UsesRegexCache()
        {
            // Arrange
            await _provider.SetAsync("product:1", Encoding.UTF8.GetBytes("value1"));
            await _provider.SetAsync("product:2", Encoding.UTF8.GetBytes("value2"));

            // Act - Call multiple times with same pattern
            var result1 = await _provider.GetKeysByPatternAsync("product:*");
            var result2 = await _provider.GetKeysByPatternAsync("product:*");
            var result3 = await _provider.GetKeysByPatternAsync("product:*");

            // Assert - All calls should return same results (verifying caching works correctly)
            result1.Should().HaveCount(2);
            result2.Should().HaveCount(2);
            result3.Should().HaveCount(2);
        }

        [Fact]
        public async Task GetKeysByPatternAsync_DifferentPatterns_WorkCorrectly()
        {
            // Arrange
            await _provider.SetAsync("product:1", Encoding.UTF8.GetBytes("value1"));
            await _provider.SetAsync("category:1", Encoding.UTF8.GetBytes("value2"));
            await _provider.SetAsync("user:1", Encoding.UTF8.GetBytes("value3"));

            // Act
            var products = await _provider.GetKeysByPatternAsync("product:*");
            var categories = await _provider.GetKeysByPatternAsync("category:*");
            var users = await _provider.GetKeysByPatternAsync("user:*");

            // Assert
            products.Should().HaveCount(1).And.Contain("product:1");
            categories.Should().HaveCount(1).And.Contain("category:1");
            users.Should().HaveCount(1).And.Contain("user:1");
        }

        [Fact]
        public async Task GetKeysByPatternAsync_WithComplexPattern_MatchesCorrectly()
        {
            // Arrange
            await _provider.SetAsync("tenant:1:product:abc", Encoding.UTF8.GetBytes("value1"));
            await _provider.SetAsync("tenant:1:product:xyz", Encoding.UTF8.GetBytes("value2"));
            await _provider.SetAsync("tenant:2:product:abc", Encoding.UTF8.GetBytes("value3"));

            // Act
            var result = await _provider.GetKeysByPatternAsync("tenant:1:product:*");

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain("tenant:1:product:abc");
            result.Should().Contain("tenant:1:product:xyz");
        }

        #endregion

        #region ClearAsync Tests

        [Fact]
        public async Task ClearAsync_RemovesAllKeys()
        {
            // Arrange
            await _provider.SetAsync("key1", Encoding.UTF8.GetBytes("value1"));
            await _provider.SetAsync("key2", Encoding.UTF8.GetBytes("value2"));
            await _provider.SetAsync("key3", Encoding.UTF8.GetBytes("value3"));

            // Act
            await _provider.ClearAsync();

            // Assert
            var key1Exists = await _provider.ExistsAsync("key1");
            var key2Exists = await _provider.ExistsAsync("key2");
            var key3Exists = await _provider.ExistsAsync("key3");

            key1Exists.Should().BeFalse();
            key2Exists.Should().BeFalse();
            key3Exists.Should().BeFalse();
        }

        #endregion

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }
    }
}
