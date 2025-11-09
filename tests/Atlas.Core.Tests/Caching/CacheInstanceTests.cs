using Atlas.Core.Services;
using Atlas.Core.Tests.Mocks;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Keys;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;

namespace Atlas.Core.Tests.Caching;

/// <summary>
/// 测试实例化缓存（带 instanceValue 的缓存）
/// </summary>
public class CacheInstanceTests
{
    private ServiceProvider CreateServiceProvider(TestCurrentUserService currentUserService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserService>(currentUserService);
        services.AddAtlasCache(options => { });
        return services.BuildServiceProvider();
    }

    [Fact]
    public async Task InstanceCache_DifferentInstances_ShouldBeSeparate()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User();
        var sp = CreateServiceProvider(userService);
        var cache = sp.GetRequiredService<ICacheService>();

        var definition = new CacheKeyDefinition(
            "OrderDetail",
            CacheKeyScope.Tenant,
            instanceKeyName: "OrderId");

        // Act - 缓存不同的订单
        await cache.SetAsync(definition, "Order1Data", instanceValue: 1);
        await cache.SetAsync(definition, "Order2Data", instanceValue: 2);
        await cache.SetAsync(definition, "Order3Data", instanceValue: 3);

        var result1 = await cache.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"), instanceValue: 1);
        var result2 = await cache.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"), instanceValue: 2);
        var result3 = await cache.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"), instanceValue: 3);

        // Assert
        result1.Should().Be("Order1Data");
        result2.Should().Be("Order2Data");
        result3.Should().Be("Order3Data");
    }

    [Fact]
    public async Task InstanceCache_SameInstance_DifferentTenants_ShouldIsolate()
    {
        // Arrange
        var definition = new CacheKeyDefinition(
            "OrderDetail",
            CacheKeyScope.Tenant,
            instanceKeyName: "OrderId");

        // 租户1
        var tenant1Service = TestCurrentUserService.CreateTenant1User();
        var sp1 = CreateServiceProvider(tenant1Service);
        var cache1 = sp1.GetRequiredService<ICacheService>();

        // 租户2
        var tenant2Service = TestCurrentUserService.CreateTenant2User();
        var sp2 = CreateServiceProvider(tenant2Service);
        var cache2 = sp2.GetRequiredService<ICacheService>();

        // Act - 两个租户都缓存 OrderId=1 的订单
        await cache1.SetAsync(definition, "Tenant1_Order1", instanceValue: 1);
        await cache2.SetAsync(definition, "Tenant2_Order1", instanceValue: 1);

        var result1 = await cache1.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"), instanceValue: 1);
        var result2 = await cache2.GetOrCreateAsync(definition, () => Task.FromResult("NotUsed"), instanceValue: 1);

        // Assert - 即使 OrderId 相同，不同租户应该有独立的缓存
        result1.Should().Be("Tenant1_Order1");
        result2.Should().Be("Tenant2_Order1");
    }

    [Fact]
    public async Task InstanceCache_RemoveSpecificInstance_ShouldNotAffectOthers()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User();
        var sp = CreateServiceProvider(userService);
        var cache = sp.GetRequiredService<ICacheService>();

        var definition = new CacheKeyDefinition("OrderDetail", instanceKeyName: "OrderId");

        await cache.SetAsync(definition, "Order1Data", instanceValue: 1);
        await cache.SetAsync(definition, "Order2Data", instanceValue: 2);

        // Act - 只删除 Order1
        await cache.RemoveAsync(definition, instanceValue: 1);

        var factoryCalled = false;
        var result1 = await cache.GetOrCreateAsync(
            definition,
            () => { factoryCalled = true; return Task.FromResult("New1"); },
            instanceValue: 1);

        var result2 = await cache.GetOrCreateAsync(
            definition,
            () => Task.FromResult("NotUsed"),
            instanceValue: 2);

        // Assert
        result1.Should().Be("New1"); // Order1 被重新加载
        factoryCalled.Should().BeTrue();
        result2.Should().Be("Order2Data"); // Order2 仍然缓存
    }
}