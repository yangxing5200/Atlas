using Atlas.Infrastructure.Caching.Abstractions;
using StackExchange.Redis;

namespace Atlas.Infrastructure.Caching.Locking;

internal sealed class RedisDistributedLock : IDistributedLock
{
    private const string ReleaseScript =
        "if redis.call('GET', KEYS[1]) == ARGV[1] then return redis.call('DEL', KEYS[1]) else return 0 end";

    private readonly IDatabase _database;
    private readonly RedisKey _key;
    private readonly RedisValue _token;
    private int _released;

    public RedisDistributedLock(
        string resource,
        IDatabase database,
        RedisKey key,
        RedisValue token)
    {
        Resource = resource;
        _database = database ?? throw new ArgumentNullException(nameof(database));
        _key = key;
        _token = token;
    }

    public bool IsAcquired => _released == 0;

    public string Resource { get; }

    public async Task ReleaseAsync()
    {
        if (Interlocked.Exchange(ref _released, 1) != 0)
            return;

        await _database.ScriptEvaluateAsync(
            ReleaseScript,
            [_key],
            [_token],
            CommandFlags.None);
    }

    public async ValueTask DisposeAsync()
    {
        await ReleaseAsync();
    }
}

/// <summary>
/// Redis-backed distributed lock provider using SET NX PX and token-checked release.
/// </summary>
public sealed class RedisDistributedLockProvider : IDistributedLockProvider
{
    private static readonly TimeSpan DefaultRetryInterval = TimeSpan.FromMilliseconds(100);
    private readonly IConnectionMultiplexer _redis;
    private readonly string _instanceName;

    public RedisDistributedLockProvider(IConnectionMultiplexer redis, string instanceName = "atlas")
    {
        _redis = redis ?? throw new ArgumentNullException(nameof(redis));
        _instanceName = string.IsNullOrWhiteSpace(instanceName) ? "atlas" : instanceName.Trim();
    }

    public async Task<IDistributedLock?> TryAcquireAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan? wait = null,
        CancellationToken ct = default)
    {
        Validate(resource, expiry);

        var database = _redis.GetDatabase();
        var key = BuildKey(resource);
        var token = Guid.NewGuid().ToString("N");
        var deadline = wait.HasValue ? DateTimeOffset.UtcNow.Add(wait.Value) : DateTimeOffset.UtcNow;

        while (!ct.IsCancellationRequested)
        {
            var acquired = await database.StringSetAsync(
                key,
                token,
                expiry,
                When.NotExists,
                CommandFlags.None);

            if (acquired)
                return new RedisDistributedLock(resource, database, key, token);

            if (!wait.HasValue || wait.Value <= TimeSpan.Zero || DateTimeOffset.UtcNow >= deadline)
                return null;

            await Task.Delay(DefaultRetryInterval, ct);
        }

        return null;
    }

    public async Task<IDistributedLock> AcquireAsync(
        string resource,
        TimeSpan expiry,
        TimeSpan? wait = null,
        TimeSpan? retry = null,
        CancellationToken ct = default)
    {
        Validate(resource, expiry);

        var retryInterval = retry ?? DefaultRetryInterval;
        var deadline = wait.HasValue ? DateTimeOffset.UtcNow.Add(wait.Value) : DateTimeOffset.MaxValue;

        while (!ct.IsCancellationRequested)
        {
            var lockHandle = await TryAcquireAsync(resource, expiry, TimeSpan.Zero, ct);
            if (lockHandle != null)
                return lockHandle;

            if (wait.HasValue && DateTimeOffset.UtcNow >= deadline)
            {
                throw new TimeoutException(
                    $"Failed to acquire Redis lock on resource '{resource}' within the specified wait period.");
            }

            await Task.Delay(retryInterval, ct);
        }

        ct.ThrowIfCancellationRequested();
        throw new OperationCanceledException(ct);
    }

    private RedisKey BuildKey(string resource)
    {
        return $"{_instanceName}:lock:{resource}";
    }

    private static void Validate(string resource, TimeSpan expiry)
    {
        if (string.IsNullOrWhiteSpace(resource))
            throw new ArgumentException("Resource cannot be null or empty.", nameof(resource));

        if (expiry <= TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(expiry), "Expiry must be positive.");
    }
}
