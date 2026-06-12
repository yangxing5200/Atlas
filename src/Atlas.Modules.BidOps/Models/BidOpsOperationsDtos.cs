using Atlas.BackgroundTasks.Operations;

namespace Atlas.Modules.BidOps.Models;

public sealed class BidOpsOperationsDashboardDto
{
    public bool BackgroundJobWorkerEnabled { get; set; }
    public bool RecurringTaskRunnerEnabled { get; set; }
    public bool BidOpsQueueConfigured { get; set; }
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
}
