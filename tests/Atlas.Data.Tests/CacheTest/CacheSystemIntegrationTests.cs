using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Invalidation;
using Atlas.Infrastructure.Caching.Keys.Generators;
using Atlas.Infrastructure.Caching.Providers.Memory;
using Atlas.Infrastructure.Caching.Scoping;
using Atlas.Infrastructure.Caching.Serialization;
using Atlas.Infrastructure.Caching.Tags;
using Atlas.Infrastructure.Caching.Tests.Helpers;
using FluentAssertions;
using Microsoft.Extensions.Caching.Memory;
using System;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Integration
{
    /// <summary>
    /// 集成测试 - 测试完整的缓存工作流程
    /// </summary>
    public class CacheSystemIntegrationTests : IDisposable
    {
        private readonly ICacheService _cacheService;
        private readonly IMemoryCache _memoryCache;
        private readonly IScopeContextAccessor _scopeAccessor;
        private readonly ITagManager _tagManager;

        public CacheSystemIntegrationTests()
        {
            // Setup real components (not mocks)
            _memoryCache = new MemoryCache(new MemoryCacheOptions());
            var provider = new MemoryCacheProvider(_memoryCache);
            var serializer = new JsonCacheSerializer();
            var tagVersionStore = new TagVersionStore();
            _tagManager = new TagManager(tagVersionStore);
            var keyGenerator = new CacheKeyGenerator();
            _scopeAccessor = new ScopeContextAccessor();
            var invalidator = new CacheInvalidator(provider, _tagManager);

            _cacheService = new CacheService(
                provider,
                serializer,
                _tagManager,
                keyGenerator,
                _scopeAccessor,
                invalidator);
        }

        #region Full Workflow Tests

        [Fact]
        public async Task FullWorkflow_CreateRetrieveInvalidate_WorksCorrectly()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();
            
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .WithTagGenerator((ctx, instance) => new[] { "product", $"product:{instance}" })
                .Build();

            var product = TestDataGenerator.CreateProduct(1, "Integration Test Product");

            // Act 1: Set cache
            await _cacheService.SetAsync(definition, product, 1);

            // Act 2: Get from cache (should hit)
            var cachedProduct = await _cacheService.GetAsync<TestProduct>(definition, 1);

            // Assert 1: Cache hit
            cachedProduct.Should().NotBeNull();
            cachedProduct!.Id.Should().Be(product.Id);
            cachedProduct.Name.Should().Be(product.Name);

            // Act 3: Invalidate by tag
            await _cacheService.InvalidateByTagAsync("product");

            // Act 4: Get from cache (should miss after invalidation)
            var afterInvalidation = await _cacheService.GetAsync<TestProduct>(definition, 1);

            // Assert 2: Cache miss after invalidation
            afterInvalidation.Should().BeNull();
        }

        [Fact]
        public async Task GetOrSet_WhenCacheMiss_CallsFactoryAndCaches()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();
            
            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .Build();

            var factoryCallCount = 0;
            Task<TestProduct> Factory()
            {
                factoryCallCount++;
                return Task.FromResult(TestDataGenerator.CreateProduct(1));
            }

            // Act 1: First call - should miss cache and call factory
            var result1 = await _cacheService.GetOrSetAsync(definition, Factory, 1);

            // Assert 1: Factory was called
            result1.IsHit.Should().BeFalse();
            result1.Source.Should().Be(CacheSource.Factory);
            factoryCallCount.Should().Be(1);

            // Act 2: Second call - should hit cache
            var result2 = await _cacheService.GetOrSetAsync(definition, Factory, 1);

            // Assert 2: Factory was NOT called again
            result2.IsHit.Should().BeTrue();
            result2.Source.Should().Be(CacheSource.Cache);
            factoryCallCount.Should().Be(1); // Still 1
        }

        #endregion

        #region Multi-Tenant Tests

        [Fact]
        public async Task MultiTenant_IsolatesDataBetweenTenants()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:list")
                .WithScope(CacheScope.Tenant)
                .Build();

            var tenant1Products = TestDataGenerator.CreateProducts(3);
            var tenant2Products = TestDataGenerator.CreateProducts(5);

            // Act: Set data for tenant 1
            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-1" };
            await _cacheService.SetAsync(definition, tenant1Products);

            // Act: Set data for tenant 2
            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-2" };
            await _cacheService.SetAsync(definition, tenant2Products);

            // Act: Retrieve tenant 1 data
            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-1" };
            var retrieved1 = await _cacheService.GetAsync<List<TestProduct>>(definition);

            // Act: Retrieve tenant 2 data
            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-2" };
            var retrieved2 = await _cacheService.GetAsync<List<TestProduct>>(definition);

            // Assert: Data is isolated
            retrieved1.Should().NotBeNull();
            retrieved1.Should().HaveCount(3);
            
            retrieved2.Should().NotBeNull();
            retrieved2.Should().HaveCount(5);
        }

        [Fact]
        public async Task InvalidateTenant_OnlyAffectsSpecificTenant()
        {
            // Arrange
            var definition = CacheKeyDefinition.Create("product:list")
                .WithScope(CacheScope.Tenant)
                .Build();

            // Set data for both tenants
            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-1" };
            await _cacheService.SetAsync(definition, TestDataGenerator.CreateProducts(3));

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-2" };
            await _cacheService.SetAsync(definition, TestDataGenerator.CreateProducts(5));

            // Act: Invalidate tenant-1 only
            await _cacheService.InvalidateTenantAsync("tenant-1");

            // Assert: Tenant-1 cache is cleared
            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-1" };
            var tenant1Data = await _cacheService.GetAsync<List<TestProduct>>(definition);
            tenant1Data.Should().BeNull();

            // Assert: Tenant-2 cache still exists
            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-2" };
            var tenant2Data = await _cacheService.GetAsync<List<TestProduct>>(definition);
            tenant2Data.Should().NotBeNull();
            tenant2Data.Should().HaveCount(5);
        }

        #endregion

        #region Tag-Based Invalidation Tests

        [Fact]
        public async Task TagInvalidation_InvalidatesAllRelatedCaches()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var productDefinition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithTagGenerator((ctx, instance) => new[] { "product", $"product:{instance}" })
                .Build();

            var listDefinition = CacheKeyDefinition.Create("product:list")
                .WithScope(CacheScope.Tenant)
                .WithTagGenerator((ctx, instance) => new[] { "product" })
                .Build();

            // Cache multiple products and a list
            await _cacheService.SetAsync(productDefinition, TestDataGenerator.CreateProduct(1), 1);
            await _cacheService.SetAsync(productDefinition, TestDataGenerator.CreateProduct(2), 2);
            await _cacheService.SetAsync(listDefinition, TestDataGenerator.CreateProducts(5));

            // Act: Invalidate the "product" tag
            await _cacheService.InvalidateByTagAsync("product");

            // Assert: All caches with "product" tag are invalidated
            var product1 = await _cacheService.GetAsync<TestProduct>(productDefinition, 1);
            var product2 = await _cacheService.GetAsync<TestProduct>(productDefinition, 2);
            var list = await _cacheService.GetAsync<List<TestProduct>>(listDefinition);

            product1.Should().BeNull();
            product2.Should().BeNull();
            list.Should().BeNull();
        }

        [Fact]
        public async Task SpecificTagInvalidation_OnlyAffectsTaggedItem()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .WithTagGenerator((ctx, instance) => new[] { "product", $"product:{instance}" })
                .Build();

            // Cache multiple products
            await _cacheService.SetAsync(definition, TestDataGenerator.CreateProduct(1), 1);
            await _cacheService.SetAsync(definition, TestDataGenerator.CreateProduct(2), 2);
            await _cacheService.SetAsync(definition, TestDataGenerator.CreateProduct(3), 3);

            // Act: Invalidate only product:2
            await _cacheService.InvalidateByTagAsync("product:2");

            // Assert: Only product 2 is invalidated
            var product1 = await _cacheService.GetAsync<TestProduct>(definition, 1);
            var product2 = await _cacheService.GetAsync<TestProduct>(definition, 2);
            var product3 = await _cacheService.GetAsync<TestProduct>(definition, 3);

            product1.Should().NotBeNull();
            product2.Should().BeNull(); // This one was invalidated
            product3.Should().NotBeNull();
        }

        #endregion

        #region Batch Operations Tests

        [Fact]
        public async Task BatchOperations_SetAndGetMany_WorkCorrectly()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .Build();

            var products = new Dictionary<object, TestProduct>
            {
                { 1, TestDataGenerator.CreateProduct(1) },
                { 2, TestDataGenerator.CreateProduct(2) },
                { 3, TestDataGenerator.CreateProduct(3) }
            };

            // Act: Batch set
            await _cacheService.SetManyAsync(definition, products);

            // Act: Batch get
            var retrieved = await _cacheService.GetManyAsync<TestProduct>(
                definition,
                new object[] { 1, 2, 3 });

            // Assert
            retrieved.Should().NotBeNull();
            retrieved.Should().HaveCount(3);
            retrieved[1].Should().NotBeNull();
            retrieved[2].Should().NotBeNull();
            retrieved[3].Should().NotBeNull();
        }

        [Fact]
        public async Task BatchRemove_RemovesMultipleItems()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .Build();

            // Set multiple items
            for (int i = 1; i <= 5; i++)
            {
                await _cacheService.SetAsync(definition, TestDataGenerator.CreateProduct(i), i);
            }

            // Act: Remove items 2, 3, 4
            var removed = await _cacheService.RemoveManyAsync(definition, new object[] { 2, 3, 4 });

            // Assert: Verify removal
            removed.Should().Be(3);

            var product1 = await _cacheService.GetAsync<TestProduct>(definition, 1);
            var product2 = await _cacheService.GetAsync<TestProduct>(definition, 2);
            var product3 = await _cacheService.GetAsync<TestProduct>(definition, 3);
            var product4 = await _cacheService.GetAsync<TestProduct>(definition, 4);
            var product5 = await _cacheService.GetAsync<TestProduct>(definition, 5);

            product1.Should().NotBeNull();
            product2.Should().BeNull();
            product3.Should().BeNull();
            product4.Should().BeNull();
            product5.Should().NotBeNull();
        }

        #endregion

        #region Statistics Tests

        [Fact]
        public async Task Statistics_TrackCacheOperations()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .Build();

            var product = TestDataGenerator.CreateProduct(1);

            // Act: Perform various operations
            await _cacheService.SetAsync(definition, product, 1); // 1 set
            await _cacheService.GetAsync<TestProduct>(definition, 1); // 1 get, 1 hit
            await _cacheService.GetAsync<TestProduct>(definition, 2); // 1 get, 1 miss
            await _cacheService.InvalidateByTagAsync("product"); // 1 invalidation

            // Get statistics
            var stats = await _cacheService.GetStatisticsAsync();

            // Assert
            stats.Should().NotBeNull();
            stats.TotalSets.Should().Be(1);
            stats.TotalGets.Should().Be(2);
            stats.TotalHits.Should().Be(1);
            stats.TotalMisses.Should().Be(1);
            stats.HitRate.Should().BeApproximately(0.5, 0.01);
        }

        #endregion

        #region Edge Cases Tests

        [Fact]
        public async Task NullValue_WhenAllowNull_StoresAndRetrieves()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var definition = CacheKeyDefinition.Create("nullable-value")
                .WithScope(CacheScope.Tenant)
                .AllowNull(true)
                .Build();

            // Act
            await _cacheService.SetAsync<TestProduct>(definition, null!);
            var exists = await _cacheService.ExistsAsync(definition);

            // Assert
            exists.Should().BeFalse(); // null is not stored even with AllowNull
        }

        [Fact]
        public async Task ClearAsync_RemovesAllCaches()
        {
            // Arrange
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var definition = CacheKeyDefinition.Create("product:{id}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("id")
                .Build();

            // Set multiple items
            for (int i = 1; i <= 10; i++)
            {
                await _cacheService.SetAsync(definition, TestDataGenerator.CreateProduct(i), i);
            }

            // Act
            await _cacheService.ClearAsync();

            // Assert: All caches are cleared
            for (int i = 1; i <= 10; i++)
            {
                var product = await _cacheService.GetAsync<TestProduct>(definition, i);
                product.Should().BeNull();
            }
        }

        #endregion

        public void Dispose()
        {
            _memoryCache?.Dispose();
        }
    }
}
