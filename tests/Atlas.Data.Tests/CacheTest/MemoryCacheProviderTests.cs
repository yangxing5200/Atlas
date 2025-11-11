// Providers/MemoryCacheProviderTests.cs
using System;
using System.Linq;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Providers.Memory;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using Xunit;

namespace Atlas.Data.Tests.Providers
{
    public class MemoryCacheProviderTests : IDisposable
    {
        private readonly IMemoryCache _memoryCache;
        private readonly MemoryCacheProvider _sut;

        public MemoryCacheProviderTests()
        {
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            _sut = new MemoryCacheProvider(_memoryCache);
        }

        [Fact]
        public async Task GetAsync_WhenKeyExists_ReturnsValue()
        {
            // Arrange
            var key = "test-key";
            var value = new byte[] { 1, 2, 3, 4 };
            await _sut.SetAsync(key, value);

            // Act
            var result = await _sut.GetAsync(key);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(value);
        }

        [Fact]
        public async Task GetAsync_WhenKeyDoesNotExist_ReturnsNull()
        {
            // Arrange
            var key = "nonexistent-key";

            // Act
            var result = await _sut.GetAsync(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_StoresValue()
        {
            // Arrange
            var key = "test-key";
            var value = new byte[] { 1, 2, 3 };

            // Act
            await _sut.SetAsync(key, value);
            var result = await _sut.GetAsync(key);

            // Assert
            result.Should().BeEquivalentTo(value);
        }

        [Fact]
        public async Task SetAsync_WithExpiration_ExpiresAfterTime()
        {
            // Arrange
            var key = "test-key";
            var value = new byte[] { 1, 2, 3 };
            var expiration = TimeSpan.FromMilliseconds(100);

            // Act
            await _sut.SetAsync(key, value, expiration);
            await Task.Delay(200);
            var result = await _sut.GetAsync(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task RemoveAsync_RemovesKey()
        {
            // Arrange
            var key = "test-key";
            var value = new byte[] { 1, 2, 3 };
            await _sut.SetAsync(key, value);

            // Act
            var removed = await _sut.RemoveAsync(key);
            var result = await _sut.GetAsync(key);

            // Assert
            removed.Should().BeTrue();
            result.Should().BeNull();
        }

        [Fact]
        public async Task ExistsAsync_WhenKeyExists_ReturnsTrue()
        {
            // Arrange
            var key = "test-key";
            var value = new byte[] { 1, 2, 3 };
            await _sut.SetAsync(key, value);

            // Act
            var result = await _sut.ExistsAsync(key);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_WhenKeyDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var key = "nonexistent-key";

            // Act
            var result = await _sut.ExistsAsync(key);

            // Assert
            result.Should().BeFalse();
        }

        [Fact]
        public async Task GetManyAsync_ReturnsMultipleValues()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            await _sut.SetAsync("key1", new byte[] { 1 });
            await _sut.SetAsync("key2", new byte[] { 2 });
            await _sut.SetAsync("key3", new byte[] { 3 });

            // Act
            var result = await _sut.GetManyAsync(keys);

            // Assert
            result.Should().HaveCount(3);
            result["key1"].Should().BeEquivalentTo(new byte[] { 1 });
            result["key2"].Should().BeEquivalentTo(new byte[] { 2 });
            result["key3"].Should().BeEquivalentTo(new byte[] { 3 });
        }

        [Fact]
        public async Task SetManyAsync_StoresMultipleValues()
        {
            // Arrange
            var items = new Dictionary<string, byte[]>
            {
                ["key1"] = new byte[] { 1 },
                ["key2"] = new byte[] { 2 },
                ["key3"] = new byte[] { 3 }
            };

            // Act
            await _sut.SetManyAsync(items);
            var result = await _sut.GetManyAsync(items.Keys);

            // Assert
            result.Should().HaveCount(3);
            result.Should().BeEquivalentTo(items);
        }

        [Fact]
        public async Task RemoveManyAsync_RemovesMultipleKeys()
        {
            // Arrange
            var keys = new[] { "key1", "key2", "key3" };
            await _sut.SetAsync("key1", new byte[] { 1 });
            await _sut.SetAsync("key2", new byte[] { 2 });
            await _sut.SetAsync("key3", new byte[] { 3 });

            // Act
            var count = await _sut.RemoveManyAsync(keys);

            // Assert
            count.Should().Be(3);
            (await _sut.ExistsAsync("key1")).Should().BeFalse();
            (await _sut.ExistsAsync("key2")).Should().BeFalse();
            (await _sut.ExistsAsync("key3")).Should().BeFalse();
        }

        [Fact]
        public async Task GetKeysByPatternAsync_ReturnsMatchingKeys()
        {
            // Arrange
            await _sut.SetAsync("user:1", new byte[] { 1 });
            await _sut.SetAsync("user:2", new byte[] { 2 });
            await _sut.SetAsync("product:1", new byte[] { 3 });

            // Act
            var result = await _sut.GetKeysByPatternAsync("user:*");

            // Assert
            result.Should().HaveCount(2);
            result.Should().Contain("user:1");
            result.Should().Contain("user:2");
        }

        [Fact]
        public async Task ClearAsync_RemovesAllKeys()
        {
            // Arrange
            await _sut.SetAsync("key1", new byte[] { 1 });
            await _sut.SetAsync("key2", new byte[] { 2 });

            // Act
            await _sut.ClearAsync();

            // Assert
            (await _sut.ExistsAsync("key1")).Should().BeFalse();
            (await _sut.ExistsAsync("key2")).Should().BeFalse();
        }

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }
    }
}