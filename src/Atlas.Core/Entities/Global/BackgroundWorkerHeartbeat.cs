using Atlas.Core.Entities.Base;
using Atlas.Core.Entities.Interfaces;

namespace Atlas.Core.Entities.Global;

public sealed class BackgroundWorkerHeartbeat : BaseEntity, ISnowflakeId
{
    public string WorkerId { get; set; } = string.Empty;
    public string HostName { get; set; } = string.Empty;
    public int ProcessId { get; set; }
    public string RuntimeMode { get; set; } = string.Empty;
    public string QueuesJson { get; set; } = "[]";
    public bool OneTimeJobWorkerEnabled { get; set; }
    public bool RecurringTaskRunnerEnabled { get; set; }
    public long? CurrentJobId { get; set; }
    public string? CurrentJobType { get; set; }
    public string? CurrentQueue { get; set; }
    public DateTime StartedAtUtc { get; set; }
    public DateTime LastSeenAtUtc { get; set; }
}
