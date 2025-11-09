using Atlas.Core.Services;
using Atlas.Core.Tests.Mocks;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Keys;
using Microsoft.Extensions.DependencyInjection;
using FluentAssertions;
namespace Atlas.Core.Tests.Caching;

/// <summary>
/// ²âÊÔ»º´æ¼ü¹¹½¨Æ÷
/// </summary>
public class CacheKeyBuilderTests
{
    private ICacheKeyBuilder CreateKeyBuilder(TestCurrentUserService currentUserService)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserService>(currentUserService);
        services.AddAtlasCache(options => { });
        var sp = services.BuildServiceProvider();
        return sp.GetRequiredService<ICacheKeyBuilder>();
    }

    [Fact]
    public void Build_GlobalScope_ShouldGenerateCorrectKey()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User();
        var builder = CreateKeyBuilder(userService);
        var definition = new CacheKeyDefinition("Config", CacheKeyScope.Global);

        // Act
        var instance = builder.Build(definition);

        // Assert
        instance.UniqueKey.Should().Be("Atlas:Config");
    }

    [Fact]
    public void Build_TenantScope_ShouldIncludeTenantId()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User();
        var builder = CreateKeyBuilder(userService);
        var definition = new CacheKeyDefinition("Stats", CacheKeyScope.Tenant);

        // Act
        var instance = builder.Build(definition);

        // Assert
        instance.UniqueKey.Should().Be("Atlas:1:Stats");
    }

    [Fact]
    public void Build_StoreScope_ShouldIncludeTenantAndStoreId()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User(storeId: 100);
        var builder = CreateKeyBuilder(userService);
        var definition = new CacheKeyDefinition("Products", CacheKeyScope.Store);

        // Act
        var instance = builder.Build(definition);

        // Assert
        instance.UniqueKey.Should().Be("Atlas:1:100:Products");
    }

    [Fact]
    public void Build_UserScope_ShouldIncludeAllIds()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User(userId: 1000, storeId: 100);
        var builder = CreateKeyBuilder(userService);
        var definition = new CacheKeyDefinition("Cart", CacheKeyScope.User);

        // Act
        var instance = builder.Build(definition);

        // Assert
        instance.UniqueKey.Should().Be("Atlas:1:100:1000:Cart");
    }

    [Fact]
    public void Build_WithInstanceValue_ShouldAppendToKey()
    {
        // Arrange
        var userService = TestCurrentUserService.CreateTenant1User();
        var builder = CreateKeyBuilder(userService);
        var definition = new CacheKeyDefinition("Order", instanceKeyName: "OrderId");

        // Act
        var instance = builder.Build(definition, instanceValue: 123);

        // Assert
        instance.UniqueKey.Should().Be("Atlas:1:Order:123");
    }

    [Fact]
    public void Build_DifferentTenants_ShouldGenerateDifferentKeys()
    {
        // Arrange
        var definition = new CacheKeyDefinition("Stats", CacheKeyScope.Tenant);

        var tenant1Service = TestCurrentUserService.CreateTenant1User();
        var builder1 = CreateKeyBuilder(tenant1Service);

        var tenant2Service = TestCurrentUserService.CreateTenant2User();
        var builder2 = CreateKeyBuilder(tenant2Service);

        // Act
        var key1 = builder1.Build(definition);
        var key2 = builder2.Build(definition);

        // Assert
        key1.UniqueKey.Should().Be("Atlas:1:Stats");
        key2.UniqueKey.Should().Be("Atlas:2:Stats");
        key1.UniqueKey.Should().NotBe(key2.UniqueKey);
    }
}