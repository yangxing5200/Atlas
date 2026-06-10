namespace Atlas.Infrastructure.Http.Resilience;

public sealed class ExternalHttpResilienceStateRegistry
{
    private readonly object _sync = new();
    private readonly Dictionary<string, CircuitState> _circuits = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, RateLimitState> _rateLimits = new(StringComparer.OrdinalIgnoreCase);

    public CircuitLease CheckCircuit(string clientName)
    {
        lock (_sync)
        {
            if (!_circuits.TryGetValue(clientName, out var state) || state.OpenUntil <= DateTimeOffset.UtcNow)
                return CircuitLease.Closed;

            return CircuitLease.Open(state.OpenUntil - DateTimeOffset.UtcNow);
        }
    }

    public void RecordCircuitSuccess(string clientName)
    {
        lock (_sync)
        {
            _circuits.Remove(clientName);
        }
    }

    public void RecordCircuitFailure(string clientName, int failureThreshold, TimeSpan breakDuration)
    {
        lock (_sync)
        {
            var state = _circuits.TryGetValue(clientName, out var existing)
                ? existing
                : new CircuitState();

            state.ConsecutiveFailures++;
            if (state.ConsecutiveFailures >= failureThreshold)
            {
                state.OpenUntil = DateTimeOffset.UtcNow.Add(breakDuration);
                state.ConsecutiveFailures = 0;
            }

            _circuits[clientName] = state;
        }
    }

    public RateLimitLease TryAcquireRateLimit(string clientName, int permitLimit, TimeSpan window)
    {
        lock (_sync)
        {
            var now = DateTimeOffset.UtcNow;
            var state = _rateLimits.TryGetValue(clientName, out var existing)
                ? existing
                : new RateLimitState(now, 0);

            if (now - state.WindowStartedAt >= window)
                state = new RateLimitState(now, 0);

            if (state.Count >= permitLimit)
            {
                var retryAfter = window - (now - state.WindowStartedAt);
                _rateLimits[clientName] = state;
                return RateLimitLease.Rejected(retryAfter);
            }

            state.Count++;
            _rateLimits[clientName] = state;
            return RateLimitLease.Acquired;
        }
    }

    private sealed class CircuitState
    {
        public int ConsecutiveFailures { get; set; }

        public DateTimeOffset OpenUntil { get; set; }
    }

    private sealed record RateLimitState(DateTimeOffset WindowStartedAt, int Count)
    {
        public int Count { get; set; } = Count;
    }
}

public sealed record CircuitLease(bool IsOpen, TimeSpan RetryAfter)
{
    public static CircuitLease Closed { get; } = new(false, TimeSpan.Zero);

    public static CircuitLease Open(TimeSpan retryAfter)
    {
        return new CircuitLease(true, retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter);
    }
}

public sealed record RateLimitLease(bool IsAcquired, TimeSpan RetryAfter)
{
    public static RateLimitLease Acquired { get; } = new(true, TimeSpan.Zero);

    public static RateLimitLease Rejected(TimeSpan retryAfter)
    {
        return new RateLimitLease(false, retryAfter < TimeSpan.Zero ? TimeSpan.Zero : retryAfter);
    }
}
