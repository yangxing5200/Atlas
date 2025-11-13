using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Serialization;
using Atlas.Infrastructure.Caching.Tests.Helpers;
using FluentAssertions;
using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Core
{
    public class CacheServiceTests
    {
        private readonly Mock<ICacheProvider> _mockProvider;
        private readonly Mock<ITagManager> _mockTagManager;
        private readonly Mock<ICacheKeyGenerator> _mockKeyGenerator;
        private readonly Mock<IScopeContextAccessor> _mockScopeAccessor;
        private readonly Mock<ICacheInvalidator> _mockInvalidator;
        private readonly ICacheSerializer _serializer;
        private readonly CacheService _cacheService;

        public CacheServiceTests()
        {
            _mockProvider = TestHelpers.CreateMockProvider();
            _mockTagManager = new Mock<ITagManager>();
            _mockKeyGenerator = new Mock<ICacheKeyGenerator>();
            _mockScopeAccessor = TestHelpers.CreateMockScopeAccessor();
            _mockInvalidator = new Mock<ICacheInvalidator>();
            _serializer = new JsonCacheSerializer();

            // Setup default behavior for TagManager
            _mockTagManager.Setup(x => x.GetTagVersionsAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync((IEnumerable<string> tags, CancellationToken ct) =>
                {
                    return tags.ToDictionary(t => t, t => 1L);
                });

            // Setup default behavior for KeyGenerator
            _mockKeyGenerator.Setup(x => x.GenerateKey(
                    It.IsAny<string>(),
                    It.IsAny<CacheScope>(),
                    It.IsAny<IDictionary<string, string>>()))
                .Returns((string key, CacheScope scope, IDictionary<string, string>? values) =>
                {
                    if (scope == CacheScope.Global)
                        return $"G:{key}";
                    if (scope == CacheScope.Tenant && values != null && values.ContainsKey("TenantId"))
                        return $"T:{values["TenantId"]}:{key}";
                    return key;
                });

            _cacheService = new CacheService(
                _mockProvider.Object,
                _serializer,
                _mockTagManager.Object,
                _mockKeyGenerator.Object,
                _mockScopeAccessor.Object,
                _mockInvalidator.Object);
        }

        #region GetAsync Tests

        [Fact]
        public async Task GetAsync_WhenCacheHit_ReturnsValue()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            var product = TestDataGenerator.CreateProduct(1, "Test Product");
            var cachedValue = new CachedValue<TestProduct>
            {
                Value = product,
                TagVersions = new Dictionary<string, long>(),
                CachedAt = DateTime.UtcNow
            };
            var serializedData = _serializer.Serialize(cachedValue);

            _mockProvider.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(serializedData);

            // Act
            var result = await _cacheService.GetAsync<TestProduct>(definition, 1);

            // Assert
            result.Should().NotBeNull();
            result!.Id.Should().Be(product.Id);
            result.Name.Should().Be(product.Name);
            result.Price.Should().Be(product.Price);
        }

        [Fact]
        public async Task GetAsync_WhenCacheMiss_ReturnsNull()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();

            _mockProvider.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            // Act
            var result = await _cacheService.GetAsync<TestProduct>(definition, 1);

            // Assert
            result.Should().BeNull();
        }

        [Fact]
        public async Task GetAsync_WhenTagVersionChanged_ReturnsNullAndRemovesCache()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            var product = TestDataGenerator.CreateProduct();
            var cachedValue = new CachedValue<TestProduct>
            {
                Value = product,
                TagVersions = new Dictionary<string, long> { { "product", 1L } },
                CachedAt = DateTime.UtcNow
            };
            var serializedData = _serializer.Serialize(cachedValue);

            _mockProvider.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(serializedData);

            // Tag version has changed
            _mockTagManager.Setup(x => x.GetTagVersionsAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, long> { { "product", 2L } });

            // Act
            var result = await _cacheService.GetAsync<TestProduct>(definition, 1);

            // Assert
            result.Should().BeNull();
            _mockProvider.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetAsync_WithNullDefinition_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _cacheService.GetAsync<TestProduct>(null!, 1));
        }

        #endregion

        #region SetAsync Tests

        [Fact]
        public async Task SetAsync_WithValidData_StoresInCache()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            var product = TestDataGenerator.CreateProduct();

            // Act
            await _cacheService.SetAsync(definition, product, 1);

            // Assert
            _mockProvider.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task SetAsync_WithNullValueAndAllowNullFalse_DoesNotStore()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .AllowNull(false)
                .Build();

            // Act
            await _cacheService.SetAsync<TestProduct>(definition, null!, 1);

            // Assert
            _mockProvider.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()), Times.Never);
        }

        [Fact]
        public async Task SetAsync_WithTags_StoresTagVersions()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithTagGenerator((context, instance) => new[] { "product", $"product:{instance}" })
                .Build();

            var product = TestDataGenerator.CreateProduct(1);

            _mockTagManager.Setup(x => x.GetTagVersionsAsync(
                    It.IsAny<IEnumerable<string>>(),
                    It.IsAny<CancellationToken>()))
                .ReturnsAsync(new Dictionary<string, long>
                {
                    { "product", 1L },
                    { "product:1", 1L }
                });

            byte[]? capturedData = null;
            _mockProvider.Setup(x => x.SetAsync(
                    It.IsAny<string>(),
                    It.IsAny<byte[]>(),
                    It.IsAny<TimeSpan?>(),
                    It.IsAny<CancellationToken>()))
                .Callback<string, byte[], TimeSpan?, CancellationToken>((key, data, exp, ct) =>
                {
                    capturedData = data;
                })
                .Returns(Task.CompletedTask);

            // Act
            await _cacheService.SetAsync(definition, product, 1);

            // Assert
            capturedData.Should().NotBeNull();
            var cachedValue = _serializer.Deserialize<CachedValue<TestProduct>>(capturedData!);
            cachedValue.Should().NotBeNull();
            cachedValue!.TagVersions.Should().ContainKey("product");
            cachedValue.TagVersions.Should().ContainKey("product:1");
        }

        [Fact]
        public async Task SetAsync_WithNullDefinition_ThrowsArgumentNullException()
        {
            // Arrange
            var product = TestDataGenerator.CreateProduct();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _cacheService.SetAsync(null!, product, 1));
        }

        #endregion

        #region GetOrSetAsync Tests

        [Fact]
        public async Task GetOrSetAsync_WhenCacheHit_ReturnsFromCacheWithoutCallingFactory()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            var product = TestDataGenerator.CreateProduct();
            var cachedValue = new CachedValue<TestProduct>
            {
                Value = product,
                TagVersions = new Dictionary<string, long>(),
                CachedAt = DateTime.UtcNow
            };
            var serializedData = _serializer.Serialize(cachedValue);

            _mockProvider.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(serializedData);

            var factoryCalled = false;
            Task<TestProduct> Factory()
            {
                factoryCalled = true;
                return Task.FromResult(TestDataGenerator.CreateProduct(999));
            }

            // Act
            var result = await _cacheService.GetOrSetAsync(definition, Factory, 1);

            // Assert
            result.Should().NotBeNull();
            result.IsHit.Should().BeTrue();
            result.Source.Should().Be(CacheSource.Cache);
            result.Value.Should().NotBeNull();
            result.Value!.Id.Should().Be(product.Id);
            factoryCalled.Should().BeFalse();
        }

        [Fact]
        public async Task GetOrSetAsync_WhenCacheMiss_CallsFactoryAndStoresResult()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            var product = TestDataGenerator.CreateProduct();

            _mockProvider.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            Task<TestProduct> Factory() => Task.FromResult(product);

            // Act
            var result = await _cacheService.GetOrSetAsync(definition, Factory, 1);

            // Assert
            result.Should().NotBeNull();
            result.IsHit.Should().BeFalse();
            result.Source.Should().Be(CacheSource.Factory);
            result.Value.Should().NotBeNull();
            result.Value!.Id.Should().Be(product.Id);

            _mockProvider.Verify(x => x.SetAsync(
                It.IsAny<string>(),
                It.IsAny<byte[]>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task GetOrSetAsync_WithNullDefinition_ThrowsArgumentNullException()
        {
            // Arrange
            Task<TestProduct> Factory() => Task.FromResult(TestDataGenerator.CreateProduct());

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _cacheService.GetOrSetAsync(null!, Factory, 1));
        }

        [Fact]
        public async Task GetOrSetAsync_WithNullFactory_ThrowsArgumentNullException()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();

            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _cacheService.GetOrSetAsync<TestProduct>(definition, null!, 1));
        }

        #endregion

        #region RemoveAsync Tests

        [Fact]
        public async Task RemoveAsync_CallsProviderRemove()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();

            _mockProvider.Setup(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _cacheService.RemoveAsync(definition, 1);

            // Assert
            result.Should().BeTrue();
            _mockProvider.Verify(x => x.RemoveAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveAsync_WithNullDefinition_ThrowsArgumentNullException()
        {
            // Act & Assert
            await Assert.ThrowsAsync<ArgumentNullException>(() =>
                _cacheService.RemoveAsync(null!, 1));
        }

        #endregion

        #region ExistsAsync Tests

        [Fact]
        public async Task ExistsAsync_WhenKeyExists_ReturnsTrue()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();

            _mockProvider.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true);

            // Act
            var result = await _cacheService.ExistsAsync(definition, 1);

            // Assert
            result.Should().BeTrue();
        }

        [Fact]
        public async Task ExistsAsync_WhenKeyDoesNotExist_ReturnsFalse()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();

            _mockProvider.Setup(x => x.ExistsAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(false);

            // Act
            var result = await _cacheService.ExistsAsync(definition, 1);

            // Assert
            result.Should().BeFalse();
        }

        #endregion

        #region Batch Operations Tests

        [Fact]
        public async Task GetManyAsync_ReturnsMultipleValues()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            var products = TestDataGenerator.CreateProducts(3);
            var instanceValues = new object[] { 1, 2, 3 };

            var mockData = new Dictionary<string, byte[]?>();
            for (int i = 0; i < products.Count; i++)
            {
                var cachedValue = new CachedValue<TestProduct>
                {
                    Value = products[i],
                    TagVersions = new Dictionary<string, long>(),
                    CachedAt = DateTime.UtcNow
                };
                var key = $"T:{TestHelpers.TestTenantId}:product:{i + 1}";
                mockData[key] = _serializer.Serialize(cachedValue);
            }

            _mockProvider.Setup(x => x.GetManyAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(mockData);

            // Act
            var result = await _cacheService.GetManyAsync<TestProduct>(definition, instanceValues);

            // Assert
            result.Should().NotBeNull();
            result.Should().HaveCount(3);
        }

        [Fact]
        public async Task SetManyAsync_StoresMultipleValues()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            var items = new Dictionary<object, TestProduct>
            {
                { 1, TestDataGenerator.CreateProduct(1) },
                { 2, TestDataGenerator.CreateProduct(2) },
                { 3, TestDataGenerator.CreateProduct(3) }
            };

            // Act
            await _cacheService.SetManyAsync(definition, items);

            // Assert
            _mockProvider.Verify(x => x.SetManyAsync(
                It.IsAny<IDictionary<string, byte[]>>(),
                It.IsAny<TimeSpan?>(),
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task RemoveManyAsync_RemovesMultipleValues()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            var instanceValues = new object[] { 1, 2, 3 };

            _mockProvider.Setup(x => x.RemoveManyAsync(It.IsAny<IEnumerable<string>>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(3);

            // Act
            var result = await _cacheService.RemoveManyAsync(definition, instanceValues);

            // Assert
            result.Should().Be(3);
        }

        #endregion

        #region Invalidation Tests

        [Fact]
        public async Task InvalidateByTagAsync_CallsInvalidator()
        {
            // Arrange
            var tag = "product";

            // Act
            await _cacheService.InvalidateByTagAsync(tag);

            // Assert
            _mockInvalidator.Verify(x => x.InvalidateByTagAsync(tag, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateByTagsAsync_CallsInvalidator()
        {
            // Arrange
            var tags = new[] { "product", "category" };

            // Act
            await _cacheService.InvalidateByTagsAsync(tags);

            // Assert
            _mockInvalidator.Verify(x => x.InvalidateByTagsAsync(tags, It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateScopeAsync_CallsInvalidator()
        {
            // Arrange
            var scope = CacheScope.Tenant;

            // Act
            await _cacheService.InvalidateScopeAsync(scope);

            // Assert
            _mockInvalidator.Verify(x => x.InvalidateByScopeAsync(
                scope,
                null,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateTenantAsync_CallsInvalidatorWithCorrectPattern()
        {
            // Arrange
            var tenantId = "tenant-123";
            var expectedPattern = $"T:{tenantId}:*";

            // Act
            await _cacheService.InvalidateTenantAsync(tenantId);

            // Assert
            _mockInvalidator.Verify(x => x.InvalidateByPatternAsync(
                expectedPattern,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateStoreAsync_CallsInvalidatorWithCorrectPattern()
        {
            // Arrange
            var tenantId = "tenant-123";
            var storeId = "store-456";
            var expectedPattern = $"S:{tenantId}:{storeId}:*";

            // Act
            await _cacheService.InvalidateStoreAsync(tenantId, storeId);

            // Assert
            _mockInvalidator.Verify(x => x.InvalidateByPatternAsync(
                expectedPattern,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        [Fact]
        public async Task InvalidateUserAsync_CallsInvalidatorWithCorrectPattern()
        {
            // Arrange
            var tenantId = "tenant-123";
            var userId = "user-789";
            var expectedPattern = $"U:{tenantId}:{userId}:*";

            // Act
            await _cacheService.InvalidateUserAsync(tenantId, userId);

            // Assert
            _mockInvalidator.Verify(x => x.InvalidateByPatternAsync(
                expectedPattern,
                It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public async Task GetStatisticsAsync_ReturnsCorrectStatistics()
        {
            // Arrange
            var definition = TestHelpers.CreateKeyDefinition();
            
            // Perform some operations
            _mockProvider.Setup(x => x.GetAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync((byte[]?)null);

            await _cacheService.GetAsync<TestProduct>(definition, 1);  // Miss
            await _cacheService.SetAsync(definition, TestDataGenerator.CreateProduct(), 1);

            // Act
            var stats = await _cacheService.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TotalGets.Should().Be(1);
            stats.TotalSets.Should().Be(1);
            stats.TotalMisses.Should().Be(1);
        }

        #endregion

        #region ClearAsync Tests

        [Fact]
        public async Task ClearAsync_CallsProviderClear()
        {
            // Act
            await _cacheService.ClearAsync();

            // Assert
            _mockProvider.Verify(x => x.ClearAsync(It.IsAny<CancellationToken>()), Times.Once);
        }

        #endregion
    }
}
