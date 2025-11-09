using Atlas.Core.Services;
using Atlas.Infrastructure.Caching.Core;
using Atlas.Infrastructure.Caching.Extensions;
using Atlas.Infrastructure.Caching.Keys;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Atlas.Integration.Tests.Caching;

public class EndToEndCachingTests
{
    private class MockIdentity : ICurrentUserService
    {
        public long? TenantId => 1;
        public long? StoreId => 100;
        public long? UserId => 1000;

        public string UserName => "²âÊÔ";

        public bool IsAuthenticated => true;
    }

    [Fact]
    public async Task FullLifecycle_ShouldWork()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton<ICurrentUserService, MockIdentity>();
        services.AddAtlasCache(opt => { });

        var sp = services.BuildServiceProvider();
        var cache = sp.GetRequiredService<ICacheService>();

        var def = new CacheKeyDefinition("Test");
        var count = 0;

        var v1 = await cache.GetOrCreateAsync(def, () => {
            count++;
            return Task.FromResult("Value");
        });

        v1.Should().Be("Value");
        count.Should().Be(1);

        var v2 = await cache.GetOrCreateAsync(def, () => {
            count++;
            return Task.FromResult("New");
        });

        v2.Should().Be("Value");
        count.Should().Be(1);
    }
}