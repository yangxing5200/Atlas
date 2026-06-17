namespace Atlas.BackgroundTasks;

public sealed class BackgroundWorkerHeartbeatState
{
    private readonly object _syncRoot = new();

    public BackgroundWorkerHeartbeatState()
    {
        WorkerId = $"{Environment.MachineName}:{Guid.NewGuid():N}";
        HostName = Environment.MachineName;
        ProcessId = Environment.ProcessId;
        StartedAtUtc = DateTime.Now;
    }

    public string WorkerId { get; }
    public string HostName { get; }
    public int ProcessId { get; }
    public DateTime StartedAtUtc { get; }

    public long? CurrentJobId { get; private set; }
    public string? CurrentJobType { get; private set; }
    public string? CurrentQueue { get; private set; }

    public BackgroundWorkerHeartbeatSnapshot Snapshot()
    {
        lock (_syncRoot)
        {
            return new BackgroundWorkerHeartbeatSnapshot(
                WorkerId,
                HostName,
                ProcessId,
                StartedAtUtc,
                CurrentJobId,
                CurrentJobType,
                CurrentQueue);
        }
    }

    public void SetCurrentJob(long jobId, string jobType, string queue)
    {
        lock (_syncRoot)
        {
            CurrentJobId = jobId;
            CurrentJobType = jobType;
            CurrentQueue = queue;
        }
    }

    public void ClearCurrentJob(long jobId)
    {
        lock (_syncRoot)
        {
            if (CurrentJobId != jobId)
                return;

            CurrentJobId = null;
            CurrentJobType = null;
            CurrentQueue = null;
        }
    }
}

public sealed record BackgroundWorkerHeartbeatSnapshot(
    string WorkerId,
    string HostName,
    int ProcessId,
    DateTime StartedAtUtc,
    long? CurrentJobId,
    string? CurrentJobType,
    string? CurrentQueue);
