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
    private static readonly AsyncLocal<ExecutionIdentitySnapshot?> CurrentSnapshot = new();

    public ExecutionIdentitySnapshot? Current => CurrentSnapshot.Value;

    public IDisposable Begin(ExecutionIdentitySnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        var previous = CurrentSnapshot.Value;
        CurrentSnapshot.Value = snapshot;
        return new Scope(previous);
    }

    private sealed class Scope : IDisposable
    {
        private readonly ExecutionIdentitySnapshot? _previous;
        private bool _disposed;

        public Scope(ExecutionIdentitySnapshot? previous)
        {
            _previous = previous;
        }

        public void Dispose()
        {
            if (_disposed)
                return;

            CurrentSnapshot.Value = _previous;
            _disposed = true;
        }
    }
}
