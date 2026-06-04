namespace Atlas.Core.Services;

public sealed record ExecutionIdentitySnapshot(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    string? SessionId,
    bool IsAuthenticated);

public interface IExecutionIdentityAccessor
{
    ExecutionIdentitySnapshot? Current { get; }
    IDisposable Begin(ExecutionIdentitySnapshot snapshot);
}

public sealed class ExecutionIdentityAccessor : IExecutionIdentityAccessor
{
    private readonly AsyncLocal<ExecutionIdentitySnapshot?> _current = new();

    public ExecutionIdentitySnapshot? Current => _current.Value;

    public IDisposable Begin(ExecutionIdentitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        var previous = _current.Value;
        _current.Value = snapshot;
        return new Scope(this, previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly ExecutionIdentityAccessor _accessor;
        private readonly ExecutionIdentitySnapshot? _previous;
        private bool _disposed;

        public Scope(ExecutionIdentityAccessor accessor, ExecutionIdentitySnapshot? previous)
        {
            _accessor = accessor;
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            _accessor._current.Value = _previous;
            _disposed = true;
        }
    }
}
