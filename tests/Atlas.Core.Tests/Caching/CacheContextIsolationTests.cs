using Atlas.Core.Services;
using Atlas.Core.Tests.Mocks;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Keys;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
namespace Atlas.Core.Tests.Caching;

/// <summary>
/// 测试缓存在不同上下文（租户、门店）之间的隔离性
/// </summary>
public class CacheContextIsolationTests
{
    private ServiceProvider CreateServiceProvider(TestCurrentUserService currentUserService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserService>(currentUserService);
        services.AddAtlasCache(options => {
            options.RedisConnectionString = "localhost:6379";
        });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task TenantScope_DifferentTenants_ShouldIsolateCache()
    {
        // Arrange
        var definition = new CacheKeyDefinition("OrderStats", CacheKeyScope.Tenant);

        // 租户1
        var tenant1Service = TestCurrentUserService.CreateTenant1User();
        var sp1 = CreateServiceProvider(tenant1Service);
        var cache1 = sp1.GetRequiredService<ICacheService>();

        // 租户2
        var tenant2Service = TestCurrentUserService.CreateTenant2User();
        var sp2 = CreateServiceProvider(tenant2Service);
        var cache2 = sp2.GetRequiredService<ICacheService>();

        // Act
        await cache1.SetAsync(definition, "Tenant1Data");
        await cache2.SetAsync(definition, "Tenant2Data");

        var result1 = await cache1.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"));
        var result2 = await cache2.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"));

        // Assert
        result1.Should().Be("Tenant1Data");
        result2.Should().Be("Tenant2Data");
    }

    [Fact]
    public async Task StoreScope_SameTenant_DifferentStores_ShouldIsolateCache()
    {
        // Arrange
        var definition = new CacheKeyDefinition("ProductList", CacheKeyScope.Store);

        // 租户1，门店100
        var store100Service = TestCurrentUserService.CreateTenant1User(userId: 1000, storeId: 100);
        var sp100 = CreateServiceProvider(store100Service);
        var cache100 = sp100.GetRequiredService<ICacheService>();

        // 租户1，门店200
        var store200Service = TestCurrentUserService.CreateTenant1User(userId: 1000, storeId: 200);
        var sp200 = CreateServiceProvider(store200Service);
        var cache200 = sp200.GetRequiredService<ICacheService>();

        // Act
        await cache100.SetAsync(definition, "Store100Data");
        await cache200.SetAsync(definition, "Store200Data");

        var result100 = await cache100.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"));
        var result200 = await cache200.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"));

        // Assert
        result100.Should().Be("Store100Data");
        result200.Should().Be("Store200Data");
    }

    [Fact]
    public async Task UserScope_DifferentUsers_ShouldIsolateCache()
    {
        // Arrange
        var definition = new CacheKeyDefinition("UserCart", CacheKeyScope.User);

        // 用户1
        var user1Service = TestCurrentUserService.CreateTenant1User(userId: 1001, storeId: 100);
        var sp1 = CreateServiceProvider(user1Service);
        var cache1 = sp1.GetRequiredService<ICacheService>();

        // 用户2（同一门店）
        var user2Service = TestCurrentUserService.CreateTenant1User(userId: 1002, storeId: 100);
        var sp2 = CreateServiceProvider(user2Service);
        var cache2 = sp2.GetRequiredService<ICacheService>();

        // Act
        await cache1.SetAsync(definition, "User1Cart");
        await cache2.SetAsync(definition, "User2Cart");

        var result1 = await cache1.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"));
        var result2 = await cache2.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"));

        // Assert
        result1.Should().Be("User1Cart");
        result2.Should().Be("User2Cart");
    }

    [Fact]
    public async Task GlobalScope_AllUsers_ShouldShareCache()
    {
        // Arrange
        var definition = new CacheKeyDefinition("SystemConfig", CacheKeyScope.Global);

        // 不同租户的用户
        var user1Service = TestCurrentUserService.CreateTenant1User();
        var sp1 = CreateServiceProvider(user1Service);
        var cache1 = sp1.GetRequiredService<ICacheService>();

        var user2Service = TestCurrentUserService.CreateTenant2User();
        var sp2 = CreateServiceProvider(user2Service);
        var cache2 = sp2.GetRequiredService<ICacheService>();

        // Act - 用户1设置全局配置
        await cache1.SetAsync(definition, "GlobalConfigData");

        // 用户2获取（应该能获取到用户1设置的数据）
        var result = await cache2.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"));

        // Assert
        result.Should().Be("GlobalConfigData");
    }
}