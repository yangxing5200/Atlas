using Atlas.BackgroundTasks.Operations;

namespace Atlas.Modules.BidOps.Models;

public sealed class BidOpsOperationsDashboardDto
{
    public bool BackgroundJobWorkerEnabled { get; set; }
    public bool RecurringTaskRunnerEnabled { get; set; }
    public bool BidOpsQueueConfigured { get; set; }
    public BidOpsRuntimeStatusDto RuntimeStatus { get; set; } = new();
    public BidOpsAiProviderSettingsDto AiSettings { get; set; } = new();
    public BackgroundJobSummaryDto Jobs { get; set; } = new();
    public int RawNoticeCreatedToday { get; set; }
    public int ReviewTaskCreatedToday { get; set; }
    public int ParseQueuedRawNotices { get; set; }
    public int FailedRawNotices { get; set; }
    public int PendingAttachments { get; set; }
    public int FailedAttachments { get; set; }
    public List<BidOpsConfigCheckItemDto> ConfigWarnings { get; set; } = [];
    public List<BackgroundJobListItemDto> RecentFailedJobs { get; set; } = [];
}

public sealed class BidOpsDashboardSummaryDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int RawNoticeCreatedToday { get; set; }
    public int ReviewTaskCreatedToday { get; set; }
    public int PendingReviewTasks { get; set; }
    public int FormalNoticeCreatedToday { get; set; }
    public int PackageCreatedToday { get; set; }
    public int ActivePackageCount { get; set; }
    public int RejectRiskRequirementCount { get; set; }
    public int OpportunityCreatedToday { get; set; }
    public int ActiveOpportunityCount { get; set; }
    public int HighValueOpportunityCount { get; set; }
    public int OpportunityTodoCount { get; set; }
    public int DeadlineRiskCount { get; set; }
    public List<BidOpsMetricBucketDto> OpportunityStageFunnel { get; set; } = [];
    public List<BidOpsMetricBucketDto> OpportunityValueDistribution { get; set; } = [];
    public List<BidOpsDashboardTodoDto> Todos { get; set; } = [];
    public List<BidOpsDashboardDeadlineRiskDto> DeadlineRisks { get; set; } = [];
    public List<BidOpsDashboardOpportunityDto> HighValueOpportunities { get; set; } = [];
}

public sealed class BidOpsMetricBucketDto
{
    public string Code { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public int Count { get; set; }
}

public sealed class BidOpsDashboardTodoDto
{
    public string Type { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Route { get; set; } = string.Empty;
    public int Priority { get; set; }
    public DateTime? DueAtUtc { get; set; }
}

public sealed class BidOpsDashboardDeadlineRiskDto
{
    public long OpportunityId { get; set; }
    public long NoticeId { get; set; }
    public long PackageId { get; set; }
    public string OpportunityNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string ValueLevel { get; set; } = string.Empty;
    public DateTime BidDeadline { get; set; }
    public int DaysRemaining { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
}

public sealed class BidOpsDashboardOpportunityDto
{
    public long OpportunityId { get; set; }
    public long PackageId { get; set; }
    public string OpportunityNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public string ValueLevel { get; set; } = string.Empty;
    public decimal? ValueScore { get; set; }
    public decimal? EstimatedAmount { get; set; }
}

public sealed class BidOpsConfigCheckDto
{
    public bool HasError { get; set; }
    public bool HasWarning { get; set; }
    public List<BidOpsConfigCheckItemDto> Items { get; set; } = [];
}

public sealed class BidOpsConfigCheckItemDto
{
    public string Severity { get; set; } = "Info";
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class BidOpsAiProviderSettingsDto
{
    public bool Enabled { get; set; }
    public bool NoticeStagingEnabled { get; set; }
    public bool OutcomeSuppliersEnabled { get; set; }
    public string ConfiguredProvider { get; set; } = string.Empty;
    public string RuntimeProvider { get; set; } = string.Empty;
    public string EffectiveProvider { get; set; } = string.Empty;
    public string ProviderSource { get; set; } = string.Empty;
    public string EffectiveModel { get; set; } = string.Empty;
    public string ReasoningEffort { get; set; } = string.Empty;
    public string DeepSeekModel { get; set; } = string.Empty;
    public string CodexCliModel { get; set; } = string.Empty;
    public string CodexCliReasoningEffort { get; set; } = string.Empty;
    public string CodexCliModelSource { get; set; } = string.Empty;
    public string CodexCliReasoningEffortSource { get; set; } = string.Empty;
    public List<BidOpsCodexCliScenarioSettingsDto> CodexCliScenarios { get; set; } = [];
    public DateTime? UpdatedAt { get; set; }
    public string UpdatedByUserName { get; set; } = string.Empty;
    public List<BidOpsAiProviderOptionDto> Options { get; set; } = [];
}

public sealed class BidOpsCodexCliRuntimeSettingsDto
{
    public string Scenario { get; set; } = BidOpsCodexCliScenarios.Default;
    public string Model { get; set; } = string.Empty;
    public string ReasoningEffort { get; set; } = string.Empty;
}

public sealed class BidOpsCodexCliScenarioSettingsDto
{
    public string Scenario { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ReasoningEffort { get; set; } = string.Empty;
    public string ModelSource { get; set; } = string.Empty;
    public string ReasoningEffortSource { get; set; } = string.Empty;
}

public sealed class BidOpsAiProviderOptionDto
{
    public string Provider { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ReasoningEffort { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string AvailabilityMessage { get; set; } = string.Empty;
}

public sealed class BidOpsRuntimeStatusDto
{
    public bool TaskPaused { get; set; }
    public string PauseReason { get; set; } = string.Empty;
    public DateTime? PauseUpdatedAt { get; set; }
    public string PauseUpdatedByUserName { get; set; } = string.Empty;
    public DateTime? DeferredUntil { get; set; }
}

public sealed class BidOpsChannelHealthDto
{
    public long ChannelId { get; set; }
    public long SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public bool SourceEnabled { get; set; }
    public bool ChannelEnabled { get; set; }
    public bool Enabled { get; set; }
    public bool NeedLogin { get; set; }
    public string ScheduleMode { get; set; } = string.Empty;
    public int? ScanIntervalMinutes { get; set; }
    public string DailyScanTime { get; set; } = string.Empty;
    public int CrawlIntervalMinutes { get; set; }
    public DateTime? LastScanTime { get; set; }
    public DateTime? LastSuccessTime { get; set; }
    public string LastError { get; set; } = string.Empty;
    public string HealthStatus { get; set; } = string.Empty;
    public DateTime? NextDueAtUtc { get; set; }
    public int? MinutesSinceLastSuccess { get; set; }
    public int PendingJobs { get; set; }
    public int RunningJobs { get; set; }
    public int FailedJobs24h { get; set; }
    public int SucceededJobs24h { get; set; }
    public string BackfillStatus { get; set; } = string.Empty;
    public string BackfillNextCursor { get; set; } = string.Empty;
    public int BackfillScannedItemCount { get; set; }
    public int BackfillCreatedCount { get; set; }
    public int BackfillChangedCount { get; set; }
    public int BackfillDuplicateCount { get; set; }
    public int BackfillFailedItemCount { get; set; }
    public int? BackfillRemainingEstimate { get; set; }
    public string AlertLevel { get; set; } = string.Empty;
    public string AlertMessage { get; set; } = string.Empty;
}

public sealed class BidOpsCrawlProgressDto
{
    public long ChannelId { get; set; }
    public long SourceId { get; set; }
    public string SourceName { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string ChannelName { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public bool SourceEnabled { get; set; }
    public bool ChannelEnabled { get; set; }
    public string Mode { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string NextCursor { get; set; } = string.Empty;
    public string LastSuccessfulCursor { get; set; } = string.Empty;
    public DateTime? RangeStartPublishTime { get; set; }
    public DateTime? RangeEndPublishTime { get; set; }
    public DateTime? HighWatermarkPublishTime { get; set; }
    public DateTime? LowWatermarkPublishTime { get; set; }
    public int? TotalRemoteCount { get; set; }
    public int ScannedItemCount { get; set; }
    public int CreatedCount { get; set; }
    public int ChangedCount { get; set; }
    public int DuplicateCount { get; set; }
    public int FailedItemCount { get; set; }
    public int? RemainingEstimate { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? LastRunAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public string PauseReason { get; set; } = string.Empty;
    public string LastError { get; set; } = string.Empty;
    public string AlertLevel { get; set; } = string.Empty;
    public string AlertMessage { get; set; } = string.Empty;
}
