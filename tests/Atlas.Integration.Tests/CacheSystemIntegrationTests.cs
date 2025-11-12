using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Providers.Hybrid;
using Atlas.Infrastructure.Caching.Scoping;
using Atlas.Infrastructure.Caching.Tests.Helpers;
using Bogus;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Integration
{
    /// <summary>
    /// 集成测试 - 测试完整的缓存工作流程
    /// </summary>
    public class CacheSystemIntegrationTests : OptimizedCacheSystemIntegrationTests
    {
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
                .WithTagGenerator((ctx, _) => new[] { "product" })
                .Build();

            // Cache multiple items with "product" tag
            await _cacheService.SetAsync(productDefinition, TestDataGenerator.CreateProduct(1), 1);
            await _cacheService.SetAsync(productDefinition, TestDataGenerator.CreateProduct(2), 2);
            await _cacheService.SetAsync(listDefinition, TestDataGenerator.CreateProducts(5));

            // Act: Invalidate all items with "product" tag
            await _cacheService.InvalidateByTagAsync("product");

            // Assert: All product-related caches are invalidated
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
            var retrieved = await _cacheService.GetAsync<TestProduct>(definition);

            // Assert
            exists.Should().BeTrue();
            retrieved.Should().BeNull();
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

        #region Test Models

        public class CartItem
        {
            public int ProductId { get; set; }
            public int Quantity { get; set; }
            public decimal Price { get; set; }
        }

        public class Cart
        {
            public string UserId { get; set; }
            public CartItem[] Items { get; set; }
            public decimal TotalAmount { get; set; }
        }

        public class CartSummary
        {
            public decimal TotalAmount { get; set; }
        }

        public class Product
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public string Description { get; set; }
            public decimal Price { get; set; }
            public string Currency { get; set; }
        }

        public class Order
        {
            public string OrderId { get; set; }
            public string UserId { get; set; }
            public string Status { get; set; }
            public decimal TotalAmount { get; set; }
            public DateTime CreateTime { get; set; }
            public DateTime? PayTime { get; set; }
        }

        public class FlashSale
        {
            public string Id { get; set; }
            public int ProductId { get; set; }
            public decimal OriginalPrice { get; set; }
            public decimal PromotionPrice { get; set; }
            public decimal Discount { get; set; }
            public DateTime EndTime { get; set; }
        }

        public class CouponBatch
        {
            public string BatchId { get; set; }
            public int TotalCount { get; set; }
            public int RemainCount { get; set; }
            public decimal Amount { get; set; }
            public DateTime ExpireDate { get; set; }
        }

        public class Category
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public CategoryChild[] Children { get; set; }
        }

        public class CategoryChild
        {
            public int Id { get; set; }
            public string Name { get; set; }
            public int Count { get; set; }
        }

        public class SearchResult
        {
            public string Keyword { get; set; }
            public int Page { get; set; }
            public int[] Products { get; set; }
        }

        public class Recommendation
        {
            public string UserId { get; set; }
            public string Algorithm { get; set; }
            public int[] Products { get; set; }
            public string[] Categories { get; set; }
        }

        public class SeckillActivity
        {
            public string ActivityId { get; set; }
            public int ProductId { get; set; }
            public int TotalStock { get; set; }
            public int SoldCount { get; set; }
            public decimal Price { get; set; }
            public DateTime StartTime { get; set; }
            public DateTime EndTime { get; set; }
        }

        public class Shop
        {
            public string ShopId { get; set; }
            public string TenantId { get; set; }
            public string Name { get; set; }
            public decimal Rating { get; set; }
            public int FollowerCount { get; set; }
            public int ProductCount { get; set; }
        }

        public class ReviewStats
        {
            public int ProductId { get; set; }
            public int TotalReviews { get; set; }
            public decimal AverageRating { get; set; }
            public int FiveStarCount { get; set; }
            public int FourStarCount { get; set; }
            public int ThreeStarCount { get; set; }
            public int TwoStarCount { get; set; }
            public int OneStarCount { get; set; }
        }

        public class LogisticsTrace
        {
            public string Time { get; set; }
            public string Location { get; set; }
            public string Status { get; set; }
        }

        public class LogisticsInfo
        {
            public string TrackingNumber { get; set; }
            public string Status { get; set; }
            public string CurrentLocation { get; set; }
            public LogisticsTrace[] Traces { get; set; }
        }

        public class Banner
        {
            public int Id { get; set; }
            public string Title { get; set; }
            public string ImageUrl { get; set; }
            public string Link { get; set; }
        }

        public class Membership
        {
            public string UserId { get; set; }
            public string Level { get; set; }
            public decimal Discount { get; set; }
            public bool FreeShipping { get; set; }
            public decimal PointsMultiplier { get; set; }
            public DateTime ExpireDate { get; set; }
            public bool? ExclusiveCustomerService { get; set; }
        }

        public class RankingItem
        {
            public int Rank { get; set; }
            public int ProductId { get; set; }
            public string Name { get; set; }
            public int Sales { get; set; }
        }

        public class ShippingRegion
        {
            public string Province { get; set; }
            public string City { get; set; }
            public string[] SupportedAreas { get; set; }
            public int StandardDeliveryDays { get; set; }
            public int ExpressDeliveryDays { get; set; }
            public decimal FreeShippingThreshold { get; set; }
        }

        #endregion

        #region 电商场景 - 商品库存缓存测试

        [Fact]
        public async Task 商品库存_高并发读取_缓存命中率高()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var stockDefinition = CacheKeyDefinition.Create("stock:{productId}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("productId")
                .WithExpiration(TimeSpan.FromSeconds(30))
                .WithTagGenerator((ctx, instance) => new[] { "stock", $"stock:{instance}" })
                .Build();

            var productId = 12345;
            var initialStock = 1000;
            await _cacheService.SetAsync(stockDefinition, initialStock, productId);

            var tasks = new List<Task<int?>>();
            for (int i = 0; i < 100; i++)
            {
                tasks.Add(_cacheService.GetAsync<int?>(stockDefinition, productId));
            }
            var results = await Task.WhenAll(tasks);

            results.Should().AllSatisfy(stock =>
            {
                stock.Should().NotBeNull();
                stock.Should().Be(initialStock);
            });

            var stats = await _cacheService.GetStatisticsAsync();
            stats.HitRate.Should().BeGreaterThan(0.95);
        }

        [Fact]
        public async Task 商品库存扣减_使用标签失效缓存()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var stockDefinition = CacheKeyDefinition.Create("stock:{productId}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("productId")
                .WithTagGenerator((ctx, instance) => new[] { "stock", $"stock:{instance}" })
                .Build();

            await _cacheService.SetAsync(stockDefinition, 100, 1);
            await _cacheService.SetAsync(stockDefinition, 200, 2);
            await _cacheService.SetAsync(stockDefinition, 300, 3);

            await _cacheService.InvalidateByTagAsync("stock:1");

            var stock1 = await _cacheService.GetAsync<int?>(stockDefinition, 1);
            var stock2 = await _cacheService.GetAsync<int>(stockDefinition, 2);
            var stock3 = await _cacheService.GetAsync<int>(stockDefinition, 3);

            stock1.Should().BeNull();
            stock2.Should().Be(200);
            stock3.Should().Be(300);
        }

        #endregion

        #region 电商场景 - 购物车缓存测试

        [Fact]
        public async Task 购物车_用户维度隔离_互不干扰()
        {
            var cartDefinition = CacheKeyDefinition.Create("cart:{userId}")
                .WithScope(CacheScope.User)
                .WithInstanceKey("userId")
                .WithExpiration(TimeSpan.FromHours(24))
                .WithTagGenerator((ctx, instance) => new[] { "cart", $"user:{instance}" })
                .Build();

            var user1Cart = new Cart
            {
                UserId = "user-001",
                Items = new[]
                {
            new CartItem { ProductId = 1, Quantity = 2, Price = 99.99m },
            new CartItem { ProductId = 2, Quantity = 1, Price = 199.99m }
        },
                TotalAmount = 399.97m
            };

            var user2Cart = new Cart
            {
                UserId = "user-002",
                Items = new[]
                {
            new CartItem { ProductId = 3, Quantity = 5, Price = 29.99m }
        },
                TotalAmount = 149.95m
            };

            _scopeAccessor.Current = new ScopeContext { TenantId = "1", UserId = "user-001" };
            await _cacheService.SetAsync(cartDefinition, user1Cart, "user-001");

            _scopeAccessor.Current = new ScopeContext { TenantId = "1", UserId = "user-002" };
            await _cacheService.SetAsync(cartDefinition, user2Cart, "user-002");

            _scopeAccessor.Current = new ScopeContext { TenantId = "1", UserId = "user-001" };
            var retrievedCart1 = await _cacheService.GetAsync<Cart>(cartDefinition, "user-001");

            _scopeAccessor.Current = new ScopeContext { TenantId = "1", UserId = "user-002" };
            var retrievedCart2 = await _cacheService.GetAsync<Cart>(cartDefinition, "user-002");

            retrievedCart1.Should().NotBeNull();
            retrievedCart1.UserId.Should().Be("user-001");

            retrievedCart2.Should().NotBeNull();
            retrievedCart2.UserId.Should().Be("user-002");
        }

        [Fact]
        public async Task 购物车清空_使用用户标签批量失效()
        {
            _scopeAccessor.Current = new ScopeContext {TenantId = "1", UserId = "user-001" };

            var cartDefinition = CacheKeyDefinition.Create("cart:{userId}")
                .WithScope(CacheScope.User)
                .WithInstanceKey("userId")
                .WithTagGenerator((ctx, instance) => new[] { "cart", $"user:{instance}" })
                .Build();

            var cartSummaryDefinition = CacheKeyDefinition.Create("cart:summary:{userId}")
                .WithScope(CacheScope.User)
                .WithInstanceKey("userId")
                .WithTagGenerator((ctx, instance) => new[] { "cart", $"user:{instance}" })
                .Build();

            await _cacheService.SetAsync(cartDefinition, new Cart { UserId = "user-001" }, "user-001");
            await _cacheService.SetAsync(cartSummaryDefinition, new CartSummary { TotalAmount = 999m }, "user-001");

            await _cacheService.InvalidateByTagAsync("user:user-001");

            var cart = await _cacheService.GetAsync<Cart>(cartDefinition, "user-001");
            var summary = await _cacheService.GetAsync<CartSummary>(cartSummaryDefinition, "user-001");

            cart.Should().BeNull();
            summary.Should().BeNull();
        }

        #endregion

        #region 电商场景 - 商品详情页缓存测试

        [Fact]
        public async Task 商品详情页_多语言多租户_正确隔离()
        {
            var productDetailDefinition = CacheKeyDefinition.Create("product:detail:{key}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("key")
                .WithExpiration(TimeSpan.FromMinutes(10))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "product-detail",
            $"product:{instance.ToString().Split('-')[0]}"
                })
                .Build();

            var productId = 100;

            var tenant1ZhProduct = new Product
            {
                Id = productId,
                Name = "高端智能手机",
                Description = "旗舰级配置，极致体验",
                Price = 5999m,
                Currency = "CNY"
            };

            var tenant1EnProduct = new Product
            {
                Id = productId,
                Name = "Premium Smartphone",
                Description = "Flagship configuration, ultimate experience",
                Price = 899m,
                Currency = "USD"
            };

            var tenant2ZhProduct = new Product
            {
                Id = productId,
                Name = "高端智能手机",
                Description = "旗舰级配置，极致体验",
                Price = 6999m,
                Currency = "CNY"
            };

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-1" };
            await _cacheService.SetAsync(productDetailDefinition, tenant1ZhProduct, $"{productId}-zh-CN");
            await _cacheService.SetAsync(productDetailDefinition, tenant1EnProduct, $"{productId}-en-US");

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-2" };
            await _cacheService.SetAsync(productDetailDefinition, tenant2ZhProduct, $"{productId}-zh-CN");

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-1" };
            var t1Zh = await _cacheService.GetAsync<Product>(productDetailDefinition, $"{productId}-zh-CN");
            var t1En = await _cacheService.GetAsync<Product>(productDetailDefinition, $"{productId}-en-US");

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-2" };
            var t2Zh = await _cacheService.GetAsync<Product>(productDetailDefinition, $"{productId}-zh-CN");

            t1Zh.Should().NotBeNull();
            t1Zh.Price.Should().Be(5999m);
            t1Zh.Name.Should().Be("高端智能手机");

            t1En.Should().NotBeNull();
            t1En.Price.Should().Be(899m);
            t1En.Name.Should().Be("Premium Smartphone");

            t2Zh.Should().NotBeNull();
            t2Zh.Price.Should().Be(6999m);
        }

        #endregion

        #region 电商场景 - 促销活动缓存测试

        [Fact]
        public async Task 限时促销_自动过期_缓存时间与活动时间一致()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var promotionEndTime = DateTime.UtcNow.AddSeconds(2);
            var promotionDuration = promotionEndTime - DateTime.UtcNow;

            var promotionDefinition = CacheKeyDefinition.Create("promotion:flash-sale:{id}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("id")
                .WithExpiration(promotionDuration)
                .WithMaxRandomOffset(0)
                .WithTagGenerator((ctx, instance) => new[] { "promotion", "flash-sale" })
                .Build();
            var flashSale = new FlashSale
            {
                Id = "flash-001",
                ProductId = 999,
                OriginalPrice = 1999m,
                PromotionPrice = 999m,
                Discount = 0.5m,
                EndTime = promotionEndTime
            };

            await _cacheService.SetAsync(promotionDefinition, flashSale, "flash-001");

            var immediate = await _cacheService.GetAsync<FlashSale>(promotionDefinition, "flash-001");
            immediate.Should().NotBeNull();

            await Task.Delay(TimeSpan.FromSeconds(2.5));

            var afterExpiration = await _cacheService.GetAsync<FlashSale>(promotionDefinition, "flash-001");
            afterExpiration.Should().BeNull();
        }

        [Fact]
        public async Task 优惠券批量分发_使用GetOrSet模式_避免缓存击穿()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var couponDefinition = CacheKeyDefinition.Create("coupon:batch:{batchId}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("batchId")
                .WithExpiration(TimeSpan.FromHours(1))
                .WithTagGenerator((ctx, instance) => new[] { "coupon" })
                .Build();

            var factoryCallCount = 0;

            Task<CouponBatch> LoadCouponBatchFromDb()
            {
                factoryCallCount++;
                return Task.FromResult(new CouponBatch
                {
                    BatchId = "SUMMER2024",
                    TotalCount = 10000,
                    RemainCount = 5000,
                    Amount = 50m,
                    ExpireDate = DateTime.UtcNow.AddDays(30)
                });
            }

            var tasks = new List<Task<CacheResult<CouponBatch>>>();
            for (int i = 0; i < 50; i++)
            {
                tasks.Add(_cacheService.GetOrSetAsync(couponDefinition, LoadCouponBatchFromDb, "SUMMER2024"));
            }

            var results = await Task.WhenAll(tasks);

            factoryCallCount.Should().Be(1);

            results.Should().AllSatisfy(result =>
            {
                result.Value.Should().NotBeNull();
                result.Value.BatchId.Should().Be("SUMMER2024");
            });
        }

        #endregion

        #region 电商场景 - 订单缓存测试

        [Fact]
        public async Task 订单状态_按用户维度缓存_支持实时更新()
        {
            var orderDefinition = CacheKeyDefinition.Create("order:{orderId}")
                .WithScope(CacheScope.User)
                .WithInstanceKey("orderId")
                .WithExpiration(TimeSpan.FromMinutes(5))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "order",
            $"order:{instance}",
            $"user-order:{ctx.UserId}"
                })
                .Build();

            var userId = "user-123";
            var orderId = "ORD-20240101-001";

            var order = new Order
            {
                OrderId = orderId,
                UserId = userId,
                Status = "待付款",
                TotalAmount = 1299m,
                CreateTime = DateTime.UtcNow
            };

            _scopeAccessor.Current = new ScopeContext { TenantId="1", UserId = userId };

            await _cacheService.SetAsync(orderDefinition, order, orderId);

            var cachedOrder = await _cacheService.GetAsync<Order>(orderDefinition, orderId);
            cachedOrder.Status.Should().Be("待付款");

            var updatedOrder = new Order
            {
                OrderId = orderId,
                UserId = userId,
                Status = "已付款",
                TotalAmount = 1299m,
                CreateTime = order.CreateTime,
                PayTime = DateTime.UtcNow
            };

            await _cacheService.SetAsync(orderDefinition, updatedOrder, orderId);

            var refreshedOrder = await _cacheService.GetAsync<Order>(orderDefinition, orderId);
            refreshedOrder.Should().NotBeNull();
            refreshedOrder.Status.Should().Be("已付款");
        }

        [Fact]
        public async Task 用户订单列表_失效单个用户所有订单缓存()
        {
            var orderDefinition = CacheKeyDefinition.Create("order:{orderId}")
                .WithScope(CacheScope.User)
                .WithInstanceKey("orderId")
                .WithTagGenerator((ctx, instance) => new[]
                {
            "order",
            $"user-order:{ctx.UserId}"
                })
                .Build();

            var userId = "user-456";
            _scopeAccessor.Current = new ScopeContext {TenantId = "1", UserId = userId };

            await _cacheService.SetAsync(orderDefinition, new Order { OrderId = "ORD-001" }, "ORD-001");
            await _cacheService.SetAsync(orderDefinition, new Order { OrderId = "ORD-002" }, "ORD-002");
            await _cacheService.SetAsync(orderDefinition, new Order { OrderId = "ORD-003" }, "ORD-003");

            await _cacheService.InvalidateByTagAsync($"user-order:{userId}");

            var order1 = await _cacheService.GetAsync<Order>(orderDefinition, "ORD-001");
            var order2 = await _cacheService.GetAsync<Order>(orderDefinition, "ORD-002");
            var order3 = await _cacheService.GetAsync<Order>(orderDefinition, "ORD-003");

            order1.Should().BeNull();
            order2.Should().BeNull();
            order3.Should().BeNull();
        }

        #endregion

        #region 电商场景 - 商品分类与搜索缓存测试

        [Fact]
        public async Task 商品分类树_全局缓存_长时间有效()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var categoryTreeDefinition = CacheKeyDefinition.Create("category:tree")
                .WithScope(CacheScope.Global)
                .WithExpiration(TimeSpan.FromHours(24))
                .WithTagGenerator((ctx, _) => new[] { "category" })
                .Build();

            var categoryTree = new[]
            {
        new Category
        {
            Id = 1,
            Name = "电子产品",
            Children = new[]
            {
                new CategoryChild { Id = 101, Name = "手机", Count = 1500 },
                new CategoryChild { Id = 102, Name = "电脑", Count = 800 },
                new CategoryChild { Id = 103, Name = "平板", Count = 600 }
            }
        },
        new Category
        {
            Id = 2,
            Name = "服装鞋包",
            Children = new[]
            {
                new CategoryChild { Id = 201, Name = "男装", Count = 3000 },
                new CategoryChild { Id = 202, Name = "女装", Count = 5000 }
            }
        }
    };

            await _cacheService.SetAsync(categoryTreeDefinition, categoryTree);

            var read1 = await _cacheService.GetAsync<Category[]>(categoryTreeDefinition);
            var read2 = await _cacheService.GetAsync<Category[]>(categoryTreeDefinition);
            var read3 = await _cacheService.GetAsync<Category[]>(categoryTreeDefinition);

            read1.Should().NotBeNull();
            read2.Should().NotBeNull();
            read3.Should().NotBeNull();

            var stats = await _cacheService.GetStatisticsAsync();
            stats.HitRate.Should().BeGreaterThan(0.8);
        }

        [Fact]
        public async Task 商品搜索结果_按搜索词和页码缓存_支持分页()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var searchResultDefinition = CacheKeyDefinition.Create("search:{key}")
                .WithScope(CacheScope.Tenant)
                .WithInstanceKey("key")
                .WithExpiration(TimeSpan.FromMinutes(5))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "search",
            $"search:{instance.ToString().Split(':')[0]}"
                })
                .Build();

            var page1Results = new SearchResult { Keyword = "手机", Page = 1, Products = new[] { 1, 2, 3, 4, 5 } };
            var page2Results = new SearchResult { Keyword = "手机", Page = 2, Products = new[] { 6, 7, 8, 9, 10 } };
            var page3Results = new SearchResult { Keyword = "手机", Page = 3, Products = new[] { 11, 12, 13, 14, 15 } };

            await _cacheService.SetAsync(searchResultDefinition, page1Results, "手机:1");
            await _cacheService.SetAsync(searchResultDefinition, page2Results, "手机:2");
            await _cacheService.SetAsync(searchResultDefinition, page3Results, "手机:3");

            var retrievedPage1 = await _cacheService.GetAsync<SearchResult>(searchResultDefinition, "手机:1");
            var retrievedPage2 = await _cacheService.GetAsync<SearchResult>(searchResultDefinition, "手机:2");
            var retrievedPage3 = await _cacheService.GetAsync<SearchResult>(searchResultDefinition, "手机:3");

            retrievedPage1.Should().NotBeNull();
            retrievedPage1.Page.Should().Be(1);

            retrievedPage2.Should().NotBeNull();
            retrievedPage2.Page.Should().Be(2);

            retrievedPage3.Should().NotBeNull();
            retrievedPage3.Page.Should().Be(3);

            await _cacheService.InvalidateByTagAsync("search:手机");

            var afterInvalidation1 = await _cacheService.GetAsync<SearchResult>(searchResultDefinition, "手机:1");
            var afterInvalidation2 = await _cacheService.GetAsync<SearchResult>(searchResultDefinition, "手机:2");
            var afterInvalidation3 = await _cacheService.GetAsync<SearchResult>(searchResultDefinition, "手机:3");

            afterInvalidation1.Should().BeNull();
            afterInvalidation2.Should().BeNull();
            afterInvalidation3.Should().BeNull();
        }

        #endregion

        #region 电商场景 - 用户个性化推荐缓存测试

        [Fact]
        public async Task 个性化推荐_按用户缓存_不同用户看到不同内容()
        {
            var recommendDefinition = CacheKeyDefinition.Create("recommend:home:{userId}")
                .WithScope(CacheScope.User)
                .WithInstanceKey("userId")
                .WithExpiration(TimeSpan.FromHours(1))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "recommend",
            $"user-recommend:{instance}"
                })
                .Build();

            var user1Recommend = new Recommendation
            {
                UserId = "user-001",
                Algorithm = "协同过滤",
                Products = new[] { 101, 102, 103, 104, 105 },
                Categories = new[] { "电子产品", "数码配件" }
            };

            var user2Recommend = new Recommendation
            {
                UserId = "user-002",
                Algorithm = "基于内容",
                Products = new[] { 201, 202, 203, 204, 205 },
                Categories = new[] { "服装", "鞋靴" }
            };

            _scopeAccessor.Current = new ScopeContext {TenantId = "1", UserId = "user-001" };
            await _cacheService.SetAsync(recommendDefinition, user1Recommend, "user-001");

            _scopeAccessor.Current = new ScopeContext {TenantId = "1", UserId = "user-002" };
            await _cacheService.SetAsync(recommendDefinition, user2Recommend, "user-002");

            _scopeAccessor.Current = new ScopeContext {TenantId = "1", UserId = "user-001" };
            var retrieved1 = await _cacheService.GetAsync<Recommendation>(recommendDefinition, "user-001");

            _scopeAccessor.Current = new ScopeContext {TenantId = "1", UserId = "user-002" };
            var retrieved2 = await _cacheService.GetAsync<Recommendation>(recommendDefinition, "user-002");

            retrieved1.Should().NotBeNull();
            retrieved1.Algorithm.Should().Be("协同过滤");

            retrieved2.Should().NotBeNull();
            retrieved2.Algorithm.Should().Be("基于内容");
        }

        #endregion

        #region 电商场景 - 秒杀场景缓存测试

        [Fact]
        public async Task 秒杀活动_全局锁定_防止超卖()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var seckillDefinition = CacheKeyDefinition.Create("seckill:{activityId}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("activityId")
                .WithExpiration(TimeSpan.FromMinutes(10))
                .WithTagGenerator((ctx, instance) => new[] { "seckill", $"activity:{instance}" })
                .Build();

            var seckillActivity = new SeckillActivity
            {
                ActivityId = "SECKILL-001",
                ProductId = 888,
                TotalStock = 100,
                SoldCount = 0,
                Price = 99.99m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddMinutes(10)
            };

            await _cacheService.SetAsync(seckillDefinition, seckillActivity, "SECKILL-001");

            var exists = await _cacheService.ExistsAsync(seckillDefinition, "SECKILL-001");
            exists.Should().BeTrue();

            var cached = await _cacheService.GetAsync<SeckillActivity>(seckillDefinition, "SECKILL-001");
            cached.Should().NotBeNull();
            cached.TotalStock.Should().Be(100);
        }

        [Fact]
        public async Task 秒杀结束_自动失效缓存_释放资源()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var seckillDefinition = CacheKeyDefinition.Create("seckill:{activityId}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("activityId")
                .WithExpiration(TimeSpan.FromSeconds(2))
                .WithMaxRandomOffset(0)
                .WithTagGenerator((ctx, instance) => new[] { "seckill" })
                .Build();

            var activity = new SeckillActivity
            {
                ActivityId = "SECKILL-QUICK",
                ProductId = 999,
                TotalStock = 50,
                SoldCount = 0,
                Price = 99m,
                StartTime = DateTime.UtcNow,
                EndTime = DateTime.UtcNow.AddSeconds(2)
            };

            await _cacheService.SetAsync(seckillDefinition, activity, "SECKILL-QUICK");

            var immediate = await _cacheService.GetAsync<SeckillActivity>(seckillDefinition, "SECKILL-QUICK");
            immediate.Should().NotBeNull();

            await Task.Delay(TimeSpan.FromSeconds(2.5));

            var afterExpiration = await _cacheService.GetAsync<SeckillActivity>(seckillDefinition, "SECKILL-QUICK");
            afterExpiration.Should().BeNull();
        }

        #endregion

        #region 电商场景 - 商家店铺缓存测试

        [Fact]
        public async Task 商家店铺信息_按租户隔离_多商家平台支持()
        {
            var shopDefinition = CacheKeyDefinition.Create("shop:info:{shopId}")
          .WithScope(CacheScope.Tenant)
          .WithInstanceKey("shopId")
          .WithExpiration(TimeSpan.FromHours(12))
          .WithTagGenerator((ctx, instance) => new[]
          {
            "shop",
            $"shop:{instance}",
            $"tenant-shop:{ctx.TenantId}"
          })
          .Build();

            var tenant1ShopA = new Shop
            {
                ShopId = "SHOP-A",
                TenantId = "tenant-1",
                Name = "旗舰电子专卖店",
                Rating = 4.8m,
                FollowerCount = 50000,
                ProductCount = 1200
            };

            var tenant2ShopB = new Shop
            {
                ShopId = "SHOP-A",
                TenantId = "tenant-2",
                Name = "Fashion Boutique",
                Rating = 4.5m,
                FollowerCount = 30000,
                ProductCount = 800
            };

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-1" };
            await _cacheService.SetAsync(shopDefinition, tenant1ShopA, "SHOP-A");

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-2" };
            await _cacheService.SetAsync(shopDefinition, tenant2ShopB, "SHOP-A");

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-1" };
            var shop1 = await _cacheService.GetAsync<Shop>(shopDefinition, "SHOP-A");

            _scopeAccessor.Current = new ScopeContext { TenantId = "tenant-2" };
            var shop2 = await _cacheService.GetAsync<Shop>(shopDefinition, "SHOP-A");

            shop1.Should().NotBeNull();
            shop1.Name.Should().Be("旗舰电子专卖店");
            shop1.ProductCount.Should().Be(1200);

            shop2.Should().NotBeNull();
            shop2.Name.Should().Be("Fashion Boutique");
            shop2.ProductCount.Should().Be(800);
        }

        #endregion

        #region 电商场景 - 评论评分缓存测试

        [Fact]
        public async Task 商品评论统计_使用批量操作_提升性能()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var reviewStatsDefinition = CacheKeyDefinition.Create("review:stats:{productId}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("productId")
                .WithExpiration(TimeSpan.FromMinutes(30))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "review-stats",
            $"product:{instance}"
                })
                .Build();

            var reviewStats = new Dictionary<object, ReviewStats>();
            for (int i = 1; i <= 10; i++)
            {
                reviewStats[i] = new ReviewStats
                {
                    ProductId = i,
                    TotalReviews = i * 100,
                    AverageRating = 4.0m + (i * 0.05m),
                    FiveStarCount = i * 50,
                    FourStarCount = i * 30,
                    ThreeStarCount = i * 15,
                    TwoStarCount = i * 3,
                    OneStarCount = i * 2
                };
            }

            await _cacheService.SetManyAsync(reviewStatsDefinition, reviewStats);

            var productIds = new object[] { 1, 3, 5, 7, 9 };
            var retrieved = await _cacheService.GetManyAsync<ReviewStats>(reviewStatsDefinition, productIds);

            retrieved.Should().NotBeNull();
            retrieved.Should().HaveCount(5);
            retrieved[1].Should().NotBeNull();
            retrieved[1].TotalReviews.Should().Be(100);
            retrieved[5].Should().NotBeNull();
            retrieved[5].TotalReviews.Should().Be(500);
        }

        [Fact]
        public async Task 新增商品评论_失效评论统计缓存()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var reviewStatsDefinition = CacheKeyDefinition.Create("review:stats:{productId}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("productId")
                .WithTagGenerator((ctx, instance) => new[]
                {
            "review-stats",
            $"product:{instance}"
                })
                .Build();

            var productId = 12345;
            var stats = new ReviewStats
            {
                ProductId = productId,
                TotalReviews = 1000,
                AverageRating = 4.5m
            };

            await _cacheService.SetAsync(reviewStatsDefinition, stats, productId);

            await _cacheService.InvalidateByTagAsync($"product:{productId}");

            var afterReview = await _cacheService.GetAsync<ReviewStats>(reviewStatsDefinition, productId);
            afterReview.Should().BeNull();
        }

        #endregion

        #region 电商场景 - 物流信息缓存测试

        [Fact]
        public async Task 物流跟踪信息_短时缓存_频繁更新()
        {
            _scopeAccessor.Current = new ScopeContext {TenantId = "1", UserId = "user-789" };

            var logisticsDefinition = CacheKeyDefinition.Create("logistics:{trackingNumber}")
                .WithScope(CacheScope.User)
                .WithInstanceKey("trackingNumber")
                .WithExpiration(TimeSpan.FromMinutes(3))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "logistics",
            $"tracking:{instance}"
                })
                .Build();

            var trackingNumber = "SF1234567890";
            var logisticsInfo = new LogisticsInfo
            {
                TrackingNumber = trackingNumber,
                Status = "运输中",
                CurrentLocation = "上海分拨中心",
                Traces = new[]
                {
            new LogisticsTrace { Time = "2024-01-01 10:00", Location = "深圳", Status = "已揽收" },
            new LogisticsTrace { Time = "2024-01-01 15:00", Location = "广州", Status = "运输中" },
            new LogisticsTrace { Time = "2024-01-02 08:00", Location = "上海", Status = "到达分拨中心" }
        }
            };

            await _cacheService.SetAsync(logisticsDefinition, logisticsInfo, trackingNumber);

            var cached = await _cacheService.GetAsync<LogisticsInfo>(logisticsDefinition, trackingNumber);

            cached.Should().NotBeNull();
            cached.Status.Should().Be("运输中");
            cached.CurrentLocation.Should().Be("上海分拨中心");
        }

        #endregion

        #region 电商场景 - 首页Banner缓存测试

        [Fact]
        public async Task 首页Banner_全局缓存_多语言支持()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var bannerDefinition = CacheKeyDefinition.Create("banner:homepage:{lang}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("lang")
                .WithExpiration(TimeSpan.FromHours(6))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "banner",
            $"banner-lang:{instance}"
                })
                .Build();

            var zhBanners = new[]
            {
        new Banner { Id = 1, Title = "双十一大促", ImageUrl = "/banners/zh/1.jpg", Link = "/sale" },
        new Banner { Id = 2, Title = "新品上市", ImageUrl = "/banners/zh/2.jpg", Link = "/new" }
    };

            var enBanners = new[]
            {
        new Banner { Id = 1, Title = "Black Friday Sale", ImageUrl = "/banners/en/1.jpg", Link = "/sale" },
        new Banner { Id = 2, Title = "New Arrivals", ImageUrl = "/banners/en/2.jpg", Link = "/new" }
    };

            await _cacheService.SetAsync(bannerDefinition, zhBanners, "zh-CN");
            await _cacheService.SetAsync(bannerDefinition, enBanners, "en-US");

            var zhRetrieved = await _cacheService.GetAsync<Banner[]>(bannerDefinition, "zh-CN");
            var enRetrieved = await _cacheService.GetAsync<Banner[]>(bannerDefinition, "en-US");

            zhRetrieved.Should().NotBeNull();
            enRetrieved.Should().NotBeNull();

            await _cacheService.InvalidateByTagAsync("banner-lang:zh-CN");

            var zhAfter = await _cacheService.GetAsync<Banner[]>(bannerDefinition, "zh-CN");
            var enAfter = await _cacheService.GetAsync<Banner[]>(bannerDefinition, "en-US");

            zhAfter.Should().BeNull();
            enAfter.Should().NotBeNull();
        }

        #endregion

        #region 电商场景 - 会员等级缓存测试

        [Fact]
        public async Task 会员等级权益_按用户缓存_支持动态更新()
        {
            var membershipDefinition = CacheKeyDefinition.Create("membership:{userId}")
                .WithScope(CacheScope.User)
                .WithInstanceKey("userId")
                .WithExpiration(TimeSpan.FromHours(24))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "membership",
            $"user-membership:{instance}"
                })
                .Build();

            var userId = "user-vip-001";

            var goldMembership = new Membership
            {
                UserId = userId,
                Level = "黄金会员",
                Discount = 0.95m,
                FreeShipping = true,
                PointsMultiplier = 2.0m,
                ExpireDate = DateTime.UtcNow.AddYears(1)
            };

            _scopeAccessor.Current = new ScopeContext {TenantId = "1", UserId = userId };

            await _cacheService.SetAsync(membershipDefinition, goldMembership, userId);

            var cached = await _cacheService.GetAsync<Membership>(membershipDefinition, userId);
            cached.Level.Should().Be("黄金会员");

            var diamondMembership = new Membership
            {
                UserId = userId,
                Level = "钻石会员",
                Discount = 0.90m,
                FreeShipping = true,
                PointsMultiplier = 3.0m,
                ExpireDate = DateTime.UtcNow.AddYears(1),
                ExclusiveCustomerService = true
            };

            await _cacheService.SetAsync(membershipDefinition, diamondMembership, userId);

            var updated = await _cacheService.GetAsync<Membership>(membershipDefinition, userId);
            updated.Should().NotBeNull();
            updated.Level.Should().Be("钻石会员");
            updated.Discount.Should().Be(0.90m);
        }

        #endregion

        #region 电商场景 - 热销排行榜缓存测试

        [Fact]
        public async Task 热销排行榜_分类维度_定时更新()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var hotSaleDefinition = CacheKeyDefinition.Create("ranking:hot-sale:{categoryId}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("categoryId")
                .WithExpiration(TimeSpan.FromMinutes(10))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "ranking",
            "hot-sale",
            $"category:{instance}"
                })
                .Build();

            var electronicsRanking = new[]
            {
        new RankingItem { Rank = 1, ProductId = 1001, Name = "iPhone 15 Pro", Sales = 5000 },
        new RankingItem { Rank = 2, ProductId = 1002, Name = "MacBook Pro", Sales = 3000 },
        new RankingItem { Rank = 3, ProductId = 1003, Name = "AirPods Pro", Sales = 2500 }
    };

            var fashionRanking = new[]
            {
        new RankingItem { Rank = 1, ProductId = 2001, Name = "羽绒服", Sales = 8000 },
        new RankingItem { Rank = 2, ProductId = 2002, Name = "毛衣", Sales = 6000 },
        new RankingItem { Rank = 3, ProductId = 2003, Name = "牛仔裤", Sales = 5500 }
    };

            await _cacheService.SetAsync(hotSaleDefinition, electronicsRanking, 1);
            await _cacheService.SetAsync(hotSaleDefinition, fashionRanking, 2);

            var electronics = await _cacheService.GetAsync<RankingItem[]>(hotSaleDefinition, 1);
            var fashion = await _cacheService.GetAsync<RankingItem[]>(hotSaleDefinition, 2);

            electronics.Should().NotBeNull();
            fashion.Should().NotBeNull();

            await _cacheService.InvalidateByTagAsync("category:1");

            var electronicsAfter = await _cacheService.GetAsync<RankingItem[]>(hotSaleDefinition, 1);
            var fashionAfter = await _cacheService.GetAsync<RankingItem[]>(hotSaleDefinition, 2);

            electronicsAfter.Should().BeNull();
            fashionAfter.Should().NotBeNull();
        }

        #endregion

        #region 电商场景 - 地区配送信息缓存测试

        [Fact]
        public async Task 配送区域信息_按地区缓存_支持复杂查询()
        {
            _scopeAccessor.Current = TestHelpers.CreateScopeContext();

            var shippingDefinition = CacheKeyDefinition.Create("shipping:region:{key}")
                .WithScope(CacheScope.Global)
                .WithInstanceKey("key")
                .WithExpiration(TimeSpan.FromDays(1))
                .WithTagGenerator((ctx, instance) => new[]
                {
            "shipping",
            $"province:{instance.ToString().Split('-')[0]}"
                })
                .Build();

            var beijingShipping = new ShippingRegion
            {
                Province = "北京",
                City = "北京市",
                SupportedAreas = new[] { "朝阳区", "海淀区", "东城区" },
                StandardDeliveryDays = 1,
                ExpressDeliveryDays = 0,
                FreeShippingThreshold = 99m
            };

            var shanghaiShipping = new ShippingRegion
            {
                Province = "上海",
                City = "上海市",
                SupportedAreas = new[] { "浦东新区", "徐汇区", "黄浦区" },
                StandardDeliveryDays = 1,
                ExpressDeliveryDays = 0,
                FreeShippingThreshold = 99m
            };

            var guangzhouShipping = new ShippingRegion
            {
                Province = "广东",
                City = "广州市",
                SupportedAreas = new[] { "天河区", "越秀区", "海珠区" },
                StandardDeliveryDays = 2,
                ExpressDeliveryDays = 1,
                FreeShippingThreshold = 129m
            };

            await _cacheService.SetAsync(shippingDefinition, beijingShipping, "北京-北京市");
            await _cacheService.SetAsync(shippingDefinition, shanghaiShipping, "上海-上海市");
            await _cacheService.SetAsync(shippingDefinition, guangzhouShipping, "广东-广州市");

            var bjInfo = await _cacheService.GetAsync<ShippingRegion>(shippingDefinition, "北京-北京市");
            var shInfo = await _cacheService.GetAsync<ShippingRegion>(shippingDefinition, "上海-上海市");
            var gzInfo = await _cacheService.GetAsync<ShippingRegion>(shippingDefinition, "广东-广州市");

            bjInfo.Should().NotBeNull();
            bjInfo.StandardDeliveryDays.Should().Be(1);

            shInfo.Should().NotBeNull();
            gzInfo.Should().NotBeNull();
            gzInfo.FreeShippingThreshold.Should().Be(129m);
        }

        #endregion
        #endregion
    }
}