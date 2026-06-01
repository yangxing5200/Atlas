using Atlas.Infrastructure.Caching.Locking;
using FluentAssertions;
using Moq;
using StackExchange.Redis;
using Xunit;

namespace Atlas.Infrastructure.Caching.Tests.Locking;

public sealed class RedisDistributedLockProviderTests
{
    [Fact]
    public async Task TryAcquireAsync_WhenRedisGrantsLock_ReturnsHandle()
    {
        var (provider, database) = CreateProvider();
        database
            .Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                When.NotExists,
                CommandFlags.None))
            .ReturnsAsync(true);

        var lockHandle = await provider.TryAcquireAsync("resource-a", TimeSpan.FromSeconds(30));

        lockHandle.Should().NotBeNull();
        lockHandle!.Resource.Should().Be("resource-a");
        lockHandle.IsAcquired.Should().BeTrue();
    }

    [Fact]
    public async Task TryAcquireAsync_WhenRedisRejectsLock_ReturnsNull()
    {
        var (provider, database) = CreateProvider();
        database
            .Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                When.NotExists,
                CommandFlags.None))
            .ReturnsAsync(false);

        var lockHandle = await provider.TryAcquireAsync("resource-a", TimeSpan.FromSeconds(30));

        lockHandle.Should().BeNull();
    }

    [Fact]
    public async Task ReleaseAsync_UsesTokenCheckedLuaScript()
    {
        var (provider, database) = CreateProvider();
        database
            .Setup(x => x.StringSetAsync(
                It.IsAny<RedisKey>(),
                It.IsAny<RedisValue>(),
                It.IsAny<TimeSpan?>(),
                When.NotExists,
                CommandFlags.None))
            .ReturnsAsync(true);
        database
            .Setup(x => x.ScriptEvaluateAsync(
                It.Is<string>(script => script.Contains("redis.call('GET'")),
                It.Is<RedisKey[]>(keys => keys.Length == 1 && keys[0] == "atlas-test:lock:resource-a"),
                It.Is<RedisValue[]>(values => values.Length == 1 && values[0].HasValue),
                CommandFlags.None))
            .ReturnsAsync(RedisResult.Create(1));

        var lockHandle = await provider.TryAcquireAsync("resource-a", TimeSpan.FromSeconds(30));

        await lockHandle!.ReleaseAsync();

        lockHandle.IsAcquired.Should().BeFalse();
        database.VerifyAll();
    }

    private static (RedisDistributedLockProvider Provider, Mock<IDatabase> Database) CreateProvider()
    {
        var database = new Mock<IDatabase>(MockBehavior.Strict);
        var redis = new Mock<IConnectionMultiplexer>(MockBehavior.Strict);
        redis
            .Setup(x => x.GetDatabase(It.IsAny<int>(), It.IsAny<object>()))
            .Returns(database.Object);

        return (new RedisDistributedLockProvider(redis.Object, "atlas-test"), database);
    }
}
