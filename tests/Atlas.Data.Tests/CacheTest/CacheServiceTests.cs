// Core/CacheServiceTests.cs
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Core.Models;
using FluentAssertions;
using Moq;
using Xunit;

namespace Atlas.Data.Tests.CacheTest
{
    public class CacheServiceTests : TestBase
    {
        private readonly Mock<ICacheProvider> _providerMock;
        private readonly Mock<ICacheSerializer> _serializerMock;
        private readonly Mock<ITagManager> _tagManagerMock;
        private readonly Mock<ICacheKeyGenerator> _keyGeneratorMock;
        private readonly Mock<IScopeContextAccessor> _scopeAccessorMock;
        private readonly Mock<ICacheInvalidator> _invalidatorMock;
        private readonly CacheService _sut;

        public CacheServiceTests()
        {
            _providerMock = new Mock<ICacheProvider>();
            _serializerMock = new Mock<ICacheSerializer>();
            _tagManagerMock = new Mock<ITagManager>();
            _keyGeneratorMock = new Mock<ICacheKeyGenerator>();
            _scopeAccessorMock = new Mock<IScopeContextAccessor>();
            _invalidatorMock = new Mock<ICacheInvalidator>();

            _sut = new CacheService(
                _providerMock.Object,
                _serializerMock.Object,
                _tagManagerMock.Object,
                _keyGeneratorMock.Object,
                _scopeAccessorMock.Object,
                _invalidatorMock.Object
            );
        }

        [Fact]
        public async Task GetAsync_WhenCacheHit_ReturnsValue()
        {
            // Arrange
            var key = "test-key";
            var fullKey = "G:test-key";
            var expectedValue = new TestData { Id = 1, Name = "Test" };
            var serializedData = new byte[] { 1, 2, 3 };

            _keyGeneratorMock
                .Setup(x => x.GenerateKey(key, CacheScope.Global, It.IsAny<IDictionary<string, string>>()))
                .Returns(fullKey);

            _providerMock
                .Setup(x => x.GetAsync(fullKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serializedData);

            _serializerMock
                .Setup(x => x.Deserialize<TestData>(serializedData))
                .Returns(expectedValue);

            // Act
            var result = await _sut.GetAsync<TestData>(key);

            // Assert
            result.Should().NotBeNull();
            result.Should().BeEquivalentTo(expectedValue);
        }

        [Fact]
        public async Task GetAsync_WhenCacheMiss_ReturnsNull()
        {
            // Arrange
            var key = "test-key";
            var fullKey = "G:test-key";

            _keyGeneratorMock
                .Setup(x => x.GenerateKey(key, CacheScope.Global, It.IsAny<IDictionary<string, string>>()))
                .Returns(fullKey);

            _providerMock
                .Setup(x => x.GetAsync(fullKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            // Act
            var result = await _sut.GetAsync<TestData>(key);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task SetAsync_WithoutTags_StoresValue()
        {
            // Arrange
            var key = "test-key";
            var fullKey = "G:test-key";
            var value = new TestData { Id = 1, Name = "Test" };
            var serializedData = new byte[] { 1, 2, 3 };
            var options = new CacheOptions { AbsoluteExpiration = TimeSpan.FromMinutes(5) };

            _keyGeneratorMock
                .Setup(x => x.GenerateKey(key, CacheScope.Global, It.IsAny<IDictionary<string, string>>()))
                .Returns(fullKey);

            _serializerMock
                .Setup(x => x.Serialize(value))
                .Returns(serializedData);

            // Act
            await _sut.SetAsync(key, value, options);

            // Assert
            _providerMock.Verify(
                x => x.SetAsync(fullKey, serializedData, options.AbsoluteExpiration, It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task SetAsync_WithTags_AppendsTagVersions()
        {
            // Arrange
            var key = "test-key";
            var baseFullKey = "G:test-key";
            var value = new TestData { Id = 1, Name = "Test" };
            var serializedData = new byte[] { 1, 2, 3 };
            var tags = new HashSet<string> { "tag1", "tag2" };
            var options = new CacheOptions { Tags = tags };
            var tagVersions = new Dictionary<string, long> { ["tag1"] = 1, ["tag2"] = 2 };

            _keyGeneratorMock
                .Setup(x => x.GenerateKey(key, CacheScope.Global, It.IsAny<IDictionary<string, string>>()))
                .Returns(baseFullKey);

            _tagManagerMock
                .Setup(x => x.GetTagVersionsAsync(tags, It.IsAny<CancellationToken>()))
                .ReturnsAsync(tagVersions);

            _serializerMock
                .Setup(x => x.Serialize(value))
                .Returns(serializedData);

            // Act
            await _sut.SetAsync(key, value, options);

            // Assert
            _providerMock.Verify(
                x => x.SetAsync(
                    It.Is<string>(k => k.Contains(":v") && k.StartsWith(baseFullKey)),
                    serializedData,
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()
                ),
                Times.Once
            );
        }

        [Fact]
        public async Task GetOrSetAsync_WhenCacheHit_ReturnsFromCache()
        {
            // Arrange
            var key = "test-key";
            var fullKey = "G:test-key";
            var cachedValue = new TestData { Id = 1, Name = "Cached" };
            var serializedData = new byte[] { 1, 2, 3 };
            var factoryCalled = false;

            _keyGeneratorMock
                .Setup(x => x.GenerateKey(key, CacheScope.Global, It.IsAny<IDictionary<string, string>>()))
                .Returns(fullKey);

            _providerMock
                .Setup(x => x.GetAsync(fullKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(serializedData);

            _serializerMock
                .Setup(x => x.Deserialize<TestData>(serializedData))
                .Returns(cachedValue);

            // Act
            var result = await _sut.GetOrSetAsync(
                key,
                async () =>
                {
                    factoryCalled = true;
                    return await Task.FromResult(new TestData { Id = 2, Name = "Factory" });
                }
            );

            // Assert
            result.IsHit.Should().BeTrue();
            result.Value.Should().BeEquivalentTo(cachedValue);
            result.Source.Should().Be(CacheSource.Cache);
            factoryCalled.Should().BeFalse();
        }

        [Fact]
        public async Task GetOrSetAsync_WhenCacheMiss_CallsFactoryAndStores()
        {
            // Arrange
            var key = "test-key";
            var fullKey = "G:test-key";
            var factoryValue = new TestData { Id = 1, Name = "Factory" };
            var serializedData = new byte[] { 1, 2, 3 };

            _keyGeneratorMock
                .Setup(x => x.GenerateKey(key, CacheScope.Global, It.IsAny<IDictionary<string, string>>()))
                .Returns(fullKey);

            _providerMock
                .Setup(x => x.GetAsync(fullKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            _serializerMock
                .Setup(x => x.Serialize(factoryValue))
                .Returns(serializedData);

            // Act
            var result = await _sut.GetOrSetAsync(
                key,
                async () => await Task.FromResult(factoryValue)
            );

            // Assert
            result.IsHit.Should().BeFalse();
            result.Value.Should().BeEquivalentTo(factoryValue);
            result.Source.Should().Be(CacheSource.Factory);

            _providerMock.Verify(
                x => x.SetAsync(fullKey, serializedData, It.IsAny<TimeSpan?>(), It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task RemoveAsync_RemovesKeyFromCache()
        {
            // Arrange
            var key = "test-key";
            var fullKey = "G:test-key";

            _keyGeneratorMock
                .Setup(x => x.GenerateKey(key, CacheScope.Global, It.IsAny<IDictionary<string, string>>()))
                .Returns(fullKey);

            _providerMock
                .Setup(x => x.RemoveAsync(fullKey, It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _sut.RemoveAsync(key);

            // Assert
            result.Should().BeTrue();
            _providerMock.Verify(x => x.RemoveAsync(fullKey, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateByTagAsync_CallsInvalidator()
        {
            // Arrange
            var tag = "test-tag";

            // Act
            await _sut.InvalidateByTagAsync(tag);

            // Assert
            _invalidatorMock.Verify(
                x => x.InvalidateByTagAsync(tag, It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task InvalidateTenantAsync_InvalidatesWithCorrectPattern()
        {
            // Arrange
            var tenantId = "tenant123";
            var expectedPattern = $"T:{tenantId}:*";

            // Act
            await _sut.InvalidateTenantAsync(tenantId);

            // Assert
            _invalidatorMock.Verify(
                x => x.InvalidateByPatternAsync(expectedPattern, It.IsAny<CancellationToken>()),
                Times.Once
            );
        }

        [Fact]
        public async Task GetStatisticsAsync_ReturnsStatistics()
        {
            // Act
            var stats = await _sut.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TotalGets.Should().BeGreaterOrEqualTo(0);
        }

        private class TestData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
        }
    }
}