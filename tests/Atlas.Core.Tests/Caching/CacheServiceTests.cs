using Atlas.Core.Tests.Fixtures;
using Atlas.Infrastructure.Caching.Keys;
using Atlas.Infrastructure.Caching.Testing;
using FluentAssertions;
using Xunit;

namespace Atlas.Core.Tests.Caching;

public class CacheServiceTests : IClassFixture<CacheTestFixture>
{
    private readonly InMemoryCacheService _cache;

    public CacheServiceTests(CacheTestFixture fixture)
    {
        _cache = new InMemoryCacheService(fixture.KeyBuilder);
    }

    [Fact]
    public async Task GetOrCreate_CacheMiss_ShouldCallFactory()
    {
        var definition = new CacheKeyDefinition("Test");
        var called = false;
        
        var result = await _cache.GetOrCreateAsync(definition, () => {
            called = true;
            return Task.FromResult("Value");
        });

        result.Should().Be("Value");
        called.Should().BeTrue();
    }

    [Fact]
    public async Task GetOrCreate_CacheHit_ShouldNotCallFactory()
    {
        var definition = new CacheKeyDefinition("Test");
        await _cache.SetAsync(definition, "Cached");
        
        var called = false;
        var result = await _cache.GetOrCreateAsync(definition, () => {
            called = true;
            return Task.FromResult("New");
        });

        result.Should().Be("Cached");
        called.Should().BeFalse();
    }
}