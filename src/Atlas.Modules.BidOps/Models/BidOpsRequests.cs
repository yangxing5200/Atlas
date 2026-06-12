using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Models;

public class BidOpsPagedQuery
{
    public string? Keyword { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class RawNoticeSearchQuery : BidOpsPagedQuery
{
    public RawNoticeStatus? Status { get; set; }
}

public sealed class CrawlRunLogSearchQuery : BidOpsPagedQuery
{
    public long? SourceId { get; set; }
    public long? ChannelId { get; set; }
    public long? BackgroundJobId { get; set; }
    public string? Operation { get; set; }
    public string? Status { get; set; }
}

public sealed class ReviewTaskSearchQuery : BidOpsPagedQuery
{
    public ReviewTaskStatus? Status { get; set; }
}

public sealed class ProcessingFailureSearchQuery : BidOpsPagedQuery
{
}

public sealed class PackageSearchQuery : BidOpsPagedQuery
{
    public long? NoticeId { get; set; }
}

public sealed class OpportunitySearchQuery : BidOpsPagedQuery
{
    public long? NoticeId { get; set; }
    public long? PackageId { get; set; }
    public string? Stage { get; set; }
    public string? Status { get; set; }
    public bool? WatchedByMe { get; set; }
}

public class CreateCrawlSourceRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = "Mock";
    public string BaseUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 10;
    public int CrawlIntervalMinutes { get; set; } = 60;
    public int MaxRetryCount { get; set; } = 3;
    public bool NeedJsRender { get; set; }
    public bool NeedLogin { get; set; }
    public bool RespectRobots { get; set; } = true;
    public string RobotsPolicyNote { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}

public sealed class UpdateCrawlSourceRequest : CreateCrawlSourceRequest
{
}

public class CreateCrawlChannelRequest
{
    public long SourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "TenderAnnouncement";
    public string ListUrl { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public sealed class UpdateCrawlChannelRequest : CreateCrawlChannelRequest
{
}

public sealed class ImportPublicUrlRequest
{
    public long? SourceId { get; set; }
    public long? ChannelId { get; set; }
    public string DetailUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? NoticeType { get; set; }
    public string? TextContent { get; set; }
}

public sealed class ReviewDecisionRequest
{
    public string? Remark { get; set; }
}

public sealed class ReparseRawNoticeRequest
{
    public string? Reason { get; set; }
}

public sealed class CreateOpportunityRequest
{
    public long PackageId { get; set; }
    public string? Title { get; set; }
    public int Priority { get; set; } = 3;
    public decimal? EstimatedAmount { get; set; }
    public long? OwnerUserId { get; set; }
    public DateTime? NextActionAtUtc { get; set; }
    public string? Remark { get; set; }
}

public sealed class UpdateOpportunityRequest
{
    public string? Title { get; set; }
    public int? Priority { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public long? OwnerUserId { get; set; }
    public DateTime? NextActionAtUtc { get; set; }
    public string? Remark { get; set; }
}

public sealed class WatchOpportunityRequest
{
    public bool Enabled { get; set; } = true;
    public string? Remark { get; set; }
}

public sealed class AssessOpportunityRequest
{
    public decimal? ValueScore { get; set; }
    public string? ValueLevel { get; set; }
    public string? Decision { get; set; }
    public string? AssessmentSummary { get; set; }
}

public sealed class ChangeOpportunityStageRequest
{
    public string Stage { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Reason { get; set; }
}

public sealed class SupplierSearchQuery : BidOpsPagedQuery
{
    public string? Status { get; set; }
    public string? Region { get; set; }
    public string? Category { get; set; }
    public bool? EvidenceExpiringOnly { get; set; }
}

public sealed class OutcomeSupplierSearchQuery : BidOpsPagedQuery
{
    public long? RawNoticeId { get; set; }
    public long? PackageId { get; set; }
    public long? SupplierId { get; set; }
    public string? OutcomeType { get; set; }
    public string? SupplierName { get; set; }
    public string? PackageNo { get; set; }
    public string? Category { get; set; }
}

public sealed class SupplierMatchRunSearchQuery : BidOpsPagedQuery
{
    public long? PackageId { get; set; }
    public string? Status { get; set; }
}

public sealed class PursuitSearchQuery : BidOpsPagedQuery
{
    public long? PackageId { get; set; }
    public long? OpportunityId { get; set; }
    public string? Stage { get; set; }
    public string? Status { get; set; }
    public long? OwnerUserId { get; set; }
    public bool? MineOnly { get; set; }
    public bool? OverdueOnly { get; set; }
}

public sealed class CreateSupplierRequest
{
    public string Name { get; set; } = string.Empty;
    public string? UnifiedSocialCreditCode { get; set; }
    public string? Region { get; set; }
    public string? Address { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public decimal? QualityScore { get; set; }
    public string? Remark { get; set; }
}

public sealed class UpdateSupplierRequest
{
    public string? Name { get; set; }
    public string? UnifiedSocialCreditCode { get; set; }
    public string? Region { get; set; }
    public string? Address { get; set; }
    public string? ContactName { get; set; }
    public string? ContactPhone { get; set; }
    public string? ContactEmail { get; set; }
    public string? Status { get; set; }
    public decimal? QualityScore { get; set; }
    public string? Remark { get; set; }
}

public sealed class CreateSupplierContactRequest
{
    public string Name { get; set; } = string.Empty;
    public string? Role { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public bool IsPrimary { get; set; }
    public string? Remark { get; set; }
}

public sealed class CreateSupplierCapabilityRequest
{
    public string Category { get; set; } = string.Empty;
    public string? ProductLine { get; set; }
    public string? CapabilityTags { get; set; }
    public string? RegionScope { get; set; }
    public string? QualificationLevel { get; set; }
    public string? Remark { get; set; }
}

public sealed class CreateSupplierEvidenceDocumentRequest
{
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string? EvidenceNo { get; set; }
    public string? IssuedBy { get; set; }
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string? FileName { get; set; }
    public string? FileUrl { get; set; }
    public string? StorageProvider { get; set; }
    public string? StorageKey { get; set; }
    public string? Remark { get; set; }
}

public sealed class StartSupplierMatchRunRequest
{
    public int MaxSuppliers { get; set; } = 100;
    public string? CriteriaSummary { get; set; }
}

public sealed class CreateGoNoGoDecisionRequest
{
    public long? OpportunityId { get; set; }
    public long? MatchRunId { get; set; }
    public long? SupplierMatchResultId { get; set; }
    public long? SupplierId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public string? RiskSummary { get; set; }
}

public sealed class CreatePursuitRequest
{
    public long PackageId { get; set; }
    public long? OpportunityId { get; set; }
    public long? GoNoGoDecisionId { get; set; }
    public long? SupplierId { get; set; }
    public string? SupplierNameSnapshot { get; set; }
    public string? Title { get; set; }
    public int? Priority { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public DateTime? BidDeadlineAtUtc { get; set; }
    public long? OwnerUserId { get; set; }
    public string? Remark { get; set; }
}

public sealed class UpdatePursuitRequest
{
    public string? Title { get; set; }
    public int? Priority { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public DateTime? BidDeadlineAtUtc { get; set; }
    public long? OwnerUserId { get; set; }
    public int? ProgressPercent { get; set; }
    public string? RiskLevel { get; set; }
    public string? Remark { get; set; }
}

public sealed class ChangePursuitStatusRequest
{
    public string Stage { get; set; } = string.Empty;
    public string? Status { get; set; }
    public string? Reason { get; set; }
}

public sealed class CreatePursuitTaskRequest
{
    public string Title { get; set; } = string.Empty;
    public string? TaskType { get; set; }
    public int? Priority { get; set; }
    public long? OwnerUserId { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public string? Description { get; set; }
}

public sealed class UpdatePursuitTaskRequest
{
    public string? Title { get; set; }
    public string? TaskType { get; set; }
    public string? Status { get; set; }
    public int? Priority { get; set; }
    public long? OwnerUserId { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public string? Description { get; set; }
    public string? ResultNote { get; set; }
}

public sealed class CreatePursuitFollowRecordRequest
{
    public string? FollowType { get; set; }
    public string Content { get; set; } = string.Empty;
    public DateTime? NextActionAtUtc { get; set; }
}
