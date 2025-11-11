// MultiTenant/MultiTenantCacheTests.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Atlas.Infrastructure.Caching.Abstractions;
using Atlas.Infrastructure.Caching.Core.Models;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Integration.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Integration.Tests.MultiTenant
{
    [Collection("Redis")]
    public class MultiTenantCacheTests : IntegrationTestBase
    {
        private readonly RedisFixture _redisFixture;
        private ICacheService _cacheService = null!;
        private IScopeContextAccessor _scopeAccessor = null!;

        public MultiTenantCacheTests(RedisFixture redisFixture)
        {
            _redisFixture = redisFixture;
        }

        protected override void ConfigureServices(IServiceCollection services)
        {
            services.AddAtlasCaching();
            services.AddRedisCaching(_redisFixture.ConnectionString, "mt-test");
            services.AddMultiTenantCaching();
        }

        protected override Task OnInitializeAsync()
        {
            _cacheService = GetService<ICacheService>();
            _scopeAccessor = GetService<IScopeContextAccessor>();
            return _redisFixture.ClearAllAsync();
        }

        [Fact]
        public async Task TenantScope_Should_IsolateCacheByTenant()
        {
            // Arrange
            var key = "product:123";
            var tenant1Id = "tenant-001";
            var tenant2Id = "tenant-002";

            var tenant1Data = new ProductData { Id = 123, Name = "Tenant 1 Product", Price = 100 };
            var tenant2Data = new ProductData { Id = 123, Name = "Tenant 2 Product", Price = 200 };

            // Act - Set for Tenant 1
            _scopeAccessor.Current = new ScopeContext { TenantId = tenant1Id };
            await _cacheService.SetAsync(
                key,
                tenant1Data,
                new CacheOptions { Scope = CacheScope.Tenant }
            );

            // Act - Set for Tenant 2
            _scopeAccessor.Current = new ScopeContext { TenantId = tenant2Id };
            await _cacheService.SetAsync(
                key,
                tenant2Data,
                new CacheOptions { Scope = CacheScope.Tenant }
            );

            // Assert - Get from Tenant 1
            _scopeAccessor.Current = new ScopeContext { TenantId = tenant1Id };
            var result1 = await _cacheService.GetAsync<ProductData>(key);

            // Assert - Get from Tenant 2
            _scopeAccessor.Current = new ScopeContext { TenantId = tenant2Id };
            var result2 = await _cacheService.GetAsync<ProductData>(key);

            // Verify isolation
            result1.Should().NotBeNull();
            result1!.Name.Should().Be("Tenant 1 Product");
            result1.Price.Should().Be(100);

            result2.Should().NotBeNull();
            result2!.Name.Should().Be("Tenant 2 Product");
            result2.Price.Should().Be(200);
        }

        [Fact]
        public async Task StoreScope_Should_IsolateCacheByStore()
        {
            // Arrange
            var key = "inventory:item-456";
            var tenantId = "tenant-001";
            var store1Id = "store-A";
            var store2Id = "store-B";

            var store1Data = new InventoryData { ItemId = 456, Quantity = 50 };
            var store2Data = new InventoryData { ItemId = 456, Quantity = 75 };

            // Act - Set for Store 1
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, StoreId = store1Id };
            await _cacheService.SetAsync(
                key,
                store1Data,
                new CacheOptions { Scope = CacheScope.Store }
            );

            // Act - Set for Store 2
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, StoreId = store2Id };
            await _cacheService.SetAsync(
                key,
                store2Data,
                new CacheOptions { Scope = CacheScope.Store }
            );

            // Assert
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, StoreId = store1Id };
            var result1 = await _cacheService.GetAsync<InventoryData>(key);

            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, StoreId = store2Id };
            var result2 = await _cacheService.GetAsync<InventoryData>(key);

            result1!.Quantity.Should().Be(50);
            result2!.Quantity.Should().Be(75);
        }

        [Fact]
        public async Task UserScope_Should_IsolateCacheByUser()
        {
            // Arrange
            var key = "user:preferences";
            var tenantId = "tenant-001";
            var user1Id = "user-alice";
            var user2Id = "user-bob";

            var user1Prefs = new UserPreferences { Theme = "dark", Language = "en" };
            var user2Prefs = new UserPreferences { Theme = "light", Language = "zh" };

            // Act
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, UserId = user1Id };
            await _cacheService.SetAsync(
                key,
                user1Prefs,
                new CacheOptions { Scope = CacheScope.User }
            );

            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, UserId = user2Id };
            await _cacheService.SetAsync(
                key,
                user2Prefs,
                new CacheOptions { Scope = CacheScope.User }
            );

            // Assert
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, UserId = user1Id };
            var result1 = await _cacheService.GetAsync<UserPreferences>(key);

            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, UserId = user2Id };
            var result2 = await _cacheService.GetAsync<UserPreferences>(key);

            result1!.Theme.Should().Be("dark");
            result2!.Theme.Should().Be("light");
        }

        [Fact]
        public async Task InvalidateTenant_Should_OnlyInvalidateTenantCache()
        {
            // Arrange
            var key = "data:shared";
            var tenant1Id = "tenant-001";
            var tenant2Id = "tenant-002";

            _scopeAccessor.Current = new ScopeContext { TenantId = tenant1Id };
            await _cacheService.SetAsync(
                key,
                new { Value = "Tenant 1" },
                new CacheOptions { Scope = CacheScope.Tenant }
            );

            _scopeAccessor.Current = new ScopeContext { TenantId = tenant2Id };
            await _cacheService.SetAsync(
                key,
                new { Value = "Tenant 2" },
                new CacheOptions { Scope = CacheScope.Tenant }
            );

            // Act - Invalidate Tenant 1
            await _cacheService.InvalidateTenantAsync(tenant1Id);

            // Assert
            _scopeAccessor.Current = new ScopeContext { TenantId = tenant1Id };
            var result1 = await _cacheService.GetAsync<dynamic>(key);

            _scopeAccessor.Current = new ScopeContext { TenantId = tenant2Id };
            var result2 = await _cacheService.GetAsync<dynamic>(key);

            result1.Should().BeNull(); // Tenant 1 cache invalidated
            result2.Should().NotBeNull(); // Tenant 2 cache still exists
        }

        [Fact]
        public async Task InvalidateStore_Should_OnlyInvalidateStoreCache()
        {
            // Arrange
            var key = "store:data";
            var tenantId = "tenant-001";
            var store1Id = "store-A";
            var store2Id = "store-B";

            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, StoreId = store1Id };
            await _cacheService.SetAsync(
                key,
                new { Value = "Store A" },
                new CacheOptions { Scope = CacheScope.Store }
            );

            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, StoreId = store2Id };
            await _cacheService.SetAsync(
                key,
                new { Value = "Store B" },
                new CacheOptions { Scope = CacheScope.Store }
            );

            // Act
            await _cacheService.InvalidateStoreAsync(tenantId, store1Id);

            // Assert
            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, StoreId = store1Id };
            var result1 = await _cacheService.GetAsync<dynamic>(key);

            _scopeAccessor.Current = new ScopeContext { TenantId = tenantId, StoreId = store2Id };
            var result2 = await _cacheService.GetAsync<dynamic>(key);

            result1.Should().BeNull();
            result2.Should().NotBeNull();
        }

        private class ProductData
        {
            public int Id { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal Price { get; set; }
        }

        private class InventoryData
        {
            public int ItemId { get; set; }
            public int Quantity { get; set; }
        }

        private class UserPreferences
        {
            public string Theme { get; set; } = string.Empty;
            public string Language { get; set; } = string.Empty;
        }
    }
}