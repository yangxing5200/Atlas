using Atlas.Infrastructure.Caching.Dependencies;
using Atlas.Infrastructure.Caching.Keys;
using FluentAssertions;
using Xunit;

namespace Atlas.Core.Tests.Caching;

public class DependencyResolverTests
{
    public class TestEntity { public int Id { get; set; } }

    [Fact]
    public void TypeLevelDependency_ShouldResolvePattern()
    {
        var definition = new CacheKeyDefinition("Stats")
            .DependsOn<TestEntity>(DependencyLevel.Type);

        definition.Dependencies.Should().HaveCount(1);
        definition.Dependencies[0].Level.Should().Be(DependencyLevel.Type);
    }

    [Fact]
    public void InstanceLevelDependency_ShouldHaveSelector()
    {
        var definition = new CacheKeyDefinition("Detail")
            .DependsOn<TestEntity>(DependencyLevel.Instance, e => e.Id);

        definition.Dependencies[0].InstanceKeySelector.Should().NotBeNull();
    }
}