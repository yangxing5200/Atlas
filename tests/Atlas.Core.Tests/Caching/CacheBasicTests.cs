using Atlas.Core.Services;
using Atlas.Core.Tests.Mocks;
using Atlas.Infrastructure.Caching.Adapters;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Keys;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using System;
using Xunit;

namespace Atlas.Core.Tests.Caching;

/// <summary>
/// 基础缓存功能测试
/// </summary>
public class CacheBasicTests
{
    private ServiceProvider CreateServiceProvider(TestCurrentUserService currentUserService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        // 1. 注册测试用的 CurrentUserService
        services.AddSingleton<ICurrentUserService>(currentUserService);

        // 2. 添加缓存系统
        services.AddAtlasCache(options =>
        {
            options.DefaultExpirationSeconds = 60;
            options.L1CacheSizeLimitMB = 10;
            // 测试环境不使用 Redis
            options.RedisConnectionString = null;
        });

        return services.BuildServiceProvider();
    }

    [Fact] 
    public async Task GetOrCreate_FirstCall_ShouldCallFactory()
    {
        // Arrange
         var userService = TestCurrentUserService.CreateTenant1User();
        var sp = CreateServiceProvider(userService);
        var cache = sp.GetRequiredService<ICacheService>();

        var definition = new CacheKeyDefinition("TestKey", CacheKeyScope.Tenant);
         var factoryCalled = false;

        // Act
                   var result = await cache.GetOrCreateAsync(
            definition,
            () =>
            {
                factoryCalled = true;
                return Task.FromResult("TestValue");
            });

        // Assert
        result.Should().Be("TestValue");
        factoryCalled.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrCreate_SecondCall_ShouldNotCallFactory()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User();
        var sp = CreateServiceProvider(userService);
        var cache = sp.GetRequiredService<ICacheService>();

        var definition = new CacheKeyDefinition("TestKey", CacheKeyScope.Tenant);

        // 第一次调用
        await cache.GetOrCreateAsync(definition, () => Task.FromResult("CachedValue"));

        var factoryCalled = false;

        // Act - 第二次调用
        var result = await cache.GetOrCreateAsync(
            definition,
            () =>
            {
                factoryCalled = true;
                return Task.FromResult("NewValue");
            });

        // Assert
        result.Should().Be("CachedValue");
        factoryCalled.Should().BeFalse();
    }

    [Fact]
    public async Task SetAndGet_ShouldWorkCorrectly()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User();
        var sp = CreateServiceProvider(userService);
        var cache = sp.GetRequiredService<ICacheService>();

        var definition = new CacheKeyDefinition("TestKey", CacheKeyScope.Tenant);

        // Act
        await cache.SetAsync(definition, "StoredValue");

        var result = await cache.GetOrCreateAsync(
            definition,
            () => Task.FromResult("NotUsed"));

        // Assert
        result.Should().Be("StoredValue");
    }

    [Fact]
    public async Task Remove_ShouldClearCache()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User();
        var sp = CreateServiceProvider(userService);
        var cache = sp.GetRequiredService<ICacheService>();

        var definition = new CacheKeyDefinition("TestKey", CacheKeyScope.Tenant);

        await cache.SetAsync(definition, "CachedValue");

        // Act
        await cache.RemoveAsync(definition);

        var factoryCalled = false;
        var result = await cache.GetOrCreateAsync(
            definition,
            () =>
            {
                factoryCalled = true;
                return Task.FromResult("NewValue");
            });

        // Assert
        result.Should().Be("NewValue");
        factoryCalled.Should().BeTrue();
    }
}

