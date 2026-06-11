using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Models;

public sealed class CrawlSourceDto
{
    public long Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public int RateLimitPerMinute { get; set; }
    public int CrawlIntervalMinutes { get; set; }
    public int MaxRetryCount { get; set; }
    public bool NeedLogin { get; set; }
    public bool RespectRobots { get; set; }
    public string RobotsPolicyNote { get; set; } = string.Empty;
    public string PauseReason { get; set; } = string.Empty;
}

public sealed class CrawlChannelDto
{
    public long Id { get; set; }
    public long SourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public string ListUrl { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime? LastScanTime { get; set; }
    public DateTime? LastSuccessTime { get; set; }
    public string LastError { get; set; } = string.Empty;
}

public sealed class RawNoticeDto
{
    public long Id { get; set; }
    public long SourceId { get; set; }
    public long? ChannelId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public DateTime FetchTime { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string TextPreview { get; set; } = string.Empty;
    public RawNoticeStatus Status { get; set; }
    public string LastError { get; set; } = string.Empty;
}

public sealed class ReviewTaskDto
{
    public long Id { get; set; }
    public string BizType { get; set; } = string.Empty;
    public long BizId { get; set; }
    public long? RawNoticeId { get; set; }
    public string TaskTitle { get; set; } = string.Empty;
    public int Priority { get; set; }
    public ReviewTaskStatus Status { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public sealed class NoticeStagingDto
{
    public long Id { get; set; }
    public long RawNoticeId { get; set; }
    public string NoticeType { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string AgencyName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public decimal? BudgetAmount { get; set; }
    public DateTime? PublishTime { get; set; }
    public DateTime? SignupDeadline { get; set; }
    public DateTime? BidDeadline { get; set; }
    public DateTime? OpenBidTime { get; set; }
    public decimal AiConfidence { get; set; }
    public ReviewStatus ReviewStatus { get; set; }
}

public sealed class PackageStagingDto
{
    public long Id { get; set; }
    public long NoticeStagingId { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public string LotName { get; set; } = string.Empty;
    public string PackageNo { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? BudgetAmount { get; set; }
    public decimal AiConfidence { get; set; }
    public ReviewStatus ReviewStatus { get; set; }
    public List<RequirementStagingDto> Requirements { get; set; } = [];
}

public sealed class RequirementStagingDto
{
    public long Id { get; set; }
    public long PackageStagingId { get; set; }
    public string RequirementType { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsRejectRisk { get; set; }
    public string RequiredEvidenceType { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
    public string AiExplanation { get; set; } = string.Empty;
    public decimal AiConfidence { get; set; }
}

public sealed class ReviewTaskDetailDto
{
    public ReviewTaskDto Task { get; set; } = new();
    public RawNoticeDto? RawNotice { get; set; }
    public NoticeStagingDto? Notice { get; set; }
    public List<PackageStagingDto> Packages { get; set; } = [];
}

public sealed class NoticeDto
{
    public long Id { get; set; }
    public long RawNoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public decimal? BudgetAmount { get; set; }
    public DateTime? PublishTime { get; set; }
    public DateTime? BidDeadline { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class TenderPackageDto
{
    public long Id { get; set; }
    public long NoticeId { get; set; }
    public string PackageNo { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? BudgetAmount { get; set; }
    public decimal? MaxPrice { get; set; }
    public string DeliveryPlace { get; set; } = string.Empty;
    public string DeliveryPeriod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
}

public sealed class RequirementItemDto
{
    public long Id { get; set; }
    public long PackageId { get; set; }
    public string RequirementType { get; set; } = string.Empty;
    public string OriginalText { get; set; } = string.Empty;
    public bool IsMandatory { get; set; }
    public bool IsRejectRisk { get; set; }
    public string RequiredEvidenceType { get; set; } = string.Empty;
    public string RiskLevel { get; set; } = string.Empty;
}

public sealed record EnqueueJobDto(
    long JobId,
    string JobType,
    string Queue,
    bool AlreadyExists);
