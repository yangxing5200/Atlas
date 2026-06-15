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

public sealed class CrawlRunLogDto
{
    public long Id { get; set; }
    public long? SourceId { get; set; }
    public long? ChannelId { get; set; }
    public long? BackgroundJobId { get; set; }
    public string Operation { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public int? DurationMs { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
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
    public string TextContent { get; set; } = string.Empty;
    public RawNoticeStatus Status { get; set; }
    public string LastError { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class RawNoticePipelineDto
{
    public long RawNoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public RawNoticeStatus RawStatus { get; set; }
    public DateTime FetchTime { get; set; }
    public string DetailUrl { get; set; } = string.Empty;
    public int AttachmentCount { get; set; }
    public int AttachmentDownloadedCount { get; set; }
    public int AttachmentTextExtractedCount { get; set; }
    public long? ReviewTaskId { get; set; }
    public ReviewTaskStatus? ReviewTaskStatus { get; set; }
    public long? NoticeStagingId { get; set; }
    public ReviewStatus? NoticeStagingStatus { get; set; }
    public long? NoticeId { get; set; }
    public int PackageCount { get; set; }
    public int RequirementCount { get; set; }
    public List<RawNoticePipelineStepDto> Steps { get; set; } = [];
}

public sealed class RawNoticePipelineStepDto
{
    public string Code { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime? OccurredAt { get; set; }
    public int TotalCount { get; set; }
    public int SucceededCount { get; set; }
    public int FailedCount { get; set; }
    public int PendingCount { get; set; }
    public string Error { get; set; } = string.Empty;
}

public sealed class RawAttachmentDto
{
    public long Id { get; set; }
    public long RawNoticeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string FileType { get; set; } = string.Empty;
    public long? FileSize { get; set; }
    public DownloadStatus DownloadStatus { get; set; }
    public TextExtractStatus TextExtractStatus { get; set; }
    public bool HasLocalFile { get; set; }
    public bool HasExtractedText { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class RawAttachmentTextDto
{
    public long Id { get; set; }
    public long RawNoticeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string TextContent { get; set; } = string.Empty;
}

public sealed class RawAttachmentFileResult
{
    public long Id { get; set; }
    public long RawNoticeId { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string ContentType { get; set; } = "application/octet-stream";
    public long? FileSize { get; set; }
    public Stream Content { get; set; } = Stream.Null;
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
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public DateTime? SignupDeadline { get; set; }
    public DateTime? BidDeadline { get; set; }
    public DateTime? OpenBidTime { get; set; }
    public decimal AiConfidence { get; set; }
    public int PackageCount { get; set; }
    public int RequirementCount { get; set; }
    public int RejectRiskCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public DateTime? ReviewedAt { get; set; }
}

public sealed class ProcessingFailureDto
{
    public long RawNoticeId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string DetailUrl { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public DateTime FetchTime { get; set; }
    public RawNoticeStatus RawStatus { get; set; }
    public string LastError { get; set; } = string.Empty;
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

public sealed class ReviewBuyerInfoDto
{
    public long? BuyerId { get; set; }
    public string BuyerName { get; set; } = string.Empty;
    public bool Exists { get; set; }
    public bool WillCreateOnApproval { get; set; }
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string NoticeTitle { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public decimal? BudgetAmount { get; set; }
    public int PackageCount { get; set; }
}

public sealed class ReviewTaskDetailDto
{
    public ReviewTaskDto Task { get; set; } = new();
    public RawNoticeDto? RawNotice { get; set; }
    public NoticeStagingDto? Notice { get; set; }
    public ReviewBuyerInfoDto? Buyer { get; set; }
    public List<OutcomeSupplierRecordDto> OutcomeSuppliers { get; set; } = [];
    public List<PackageStagingDto> Packages { get; set; } = [];
    public List<RawAttachmentDto> Attachments { get; set; } = [];
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
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class TenderPackageDto
{
    public long Id { get; set; }
    public long NoticeId { get; set; }
    public string NoticeTitle { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public DateTime? BidDeadline { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public string LotName { get; set; } = string.Empty;
    public string PackageNo { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public decimal? Quantity { get; set; }
    public string Unit { get; set; } = string.Empty;
    public decimal? BudgetAmount { get; set; }
    public decimal? MaxPrice { get; set; }
    public string DeliveryPlace { get; set; } = string.Empty;
    public string DeliveryPeriod { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int RequirementCount { get; set; }
    public int RejectRiskCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
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

public sealed class PackageTimelineItemDto
{
    public string EventType { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public DateTime OccurredAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

public sealed class OpportunityDto
{
    public long Id { get; set; }
    public long NoticeId { get; set; }
    public long PackageId { get; set; }
    public string OpportunityNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string NoticeTitle { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string PackageNo { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public DateTime? BidDeadline { get; set; }
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public decimal? ValueScore { get; set; }
    public string ValueLevel { get; set; } = string.Empty;
    public string Decision { get; set; } = string.Empty;
    public long? OwnerUserId { get; set; }
    public DateTime? NextActionAtUtc { get; set; }
    public DateTime LastStageChangedAtUtc { get; set; }
    public string AssessmentSummary { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
    public int WatchCount { get; set; }
    public bool WatchedByMe { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class OpportunityStageHistoryDto
{
    public long Id { get; set; }
    public long OpportunityId { get; set; }
    public string FromStage { get; set; } = string.Empty;
    public string ToStage { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public long? OperatorUserId { get; set; }
    public DateTime OccurredAtUtc { get; set; }
}

public sealed class OpportunityDetailDto
{
    public OpportunityDto Opportunity { get; set; } = new();
    public TenderPackageDto? Package { get; set; }
    public List<RequirementItemDto> Requirements { get; set; } = [];
    public List<OpportunityStageHistoryDto> StageHistory { get; set; } = [];
}

public sealed class SupplierDto
{
    public long Id { get; set; }
    public string SupplierNo { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string UnifiedSocialCreditCode { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Address { get; set; } = string.Empty;
    public string ContactName { get; set; } = string.Empty;
    public string ContactPhone { get; set; } = string.Empty;
    public string ContactEmail { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal? QualityScore { get; set; }
    public string Remark { get; set; } = string.Empty;
    public int ContactCount { get; set; }
    public int CapabilityCount { get; set; }
    public int EvidenceCount { get; set; }
    public int ExpiringEvidenceCount { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class SupplierContactDto
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
    public string Phone { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public bool IsPrimary { get; set; }
    public string Remark { get; set; } = string.Empty;
}

public sealed class SupplierCapabilityDto
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public string Category { get; set; } = string.Empty;
    public string ProductLine { get; set; } = string.Empty;
    public string CapabilityTags { get; set; } = string.Empty;
    public string RegionScope { get; set; } = string.Empty;
    public string QualificationLevel { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}

public sealed class SupplierEvidenceDocumentDto
{
    public long Id { get; set; }
    public long SupplierId { get; set; }
    public string DocumentName { get; set; } = string.Empty;
    public string DocumentType { get; set; } = string.Empty;
    public string EvidenceNo { get; set; } = string.Empty;
    public string IssuedBy { get; set; } = string.Empty;
    public DateTime? ValidFrom { get; set; }
    public DateTime? ValidTo { get; set; }
    public string FileName { get; set; } = string.Empty;
    public string FileUrl { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}

public sealed class SupplierDetailDto
{
    public SupplierDto Supplier { get; set; } = new();
    public List<SupplierContactDto> Contacts { get; set; } = [];
    public List<SupplierCapabilityDto> Capabilities { get; set; } = [];
    public List<SupplierEvidenceDocumentDto> EvidenceDocuments { get; set; } = [];
}

public sealed class OutcomeSupplierRecordDto
{
    public long Id { get; set; }
    public long RawNoticeId { get; set; }
    public long? NoticeId { get; set; }
    public long? TenderPackageId { get; set; }
    public long? BuyerId { get; set; }
    public long? SupplierId { get; set; }
    public string SourceUrl { get; set; } = string.Empty;
    public string NoticeTitle { get; set; } = string.Empty;
    public string NoticeType { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public string LotNo { get; set; } = string.Empty;
    public string LotName { get; set; } = string.Empty;
    public string PackageNo { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string OutcomeType { get; set; } = string.Empty;
    public int? Rank { get; set; }
    public decimal? AwardAmount { get; set; }
    public decimal? ProcurementAgencyServiceFeeAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string EvidenceText { get; set; } = string.Empty;
    public decimal ExtractionConfidence { get; set; }
    public DateTime CreatedAt { get; set; }
}

public sealed class SupplierOutcomeStatDto
{
    public string SupplierName { get; set; } = string.Empty;
    public long? SupplierId { get; set; }
    public int OutcomeCount { get; set; }
    public int AwardedCount { get; set; }
    public int CandidateCount { get; set; }
    public decimal? TotalAwardAmount { get; set; }
    public DateTime? LastPublishTime { get; set; }
}

public sealed class SupplierOutcomeSummaryDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public int RecordCount { get; set; }
    public int SupplierCount { get; set; }
    public int AwardedCount { get; set; }
    public int CandidateCount { get; set; }
    public int LinkedPackageCount { get; set; }
    public int LinkedSupplierCount { get; set; }
    public List<SupplierOutcomeStatDto> TopSuppliers { get; set; } = [];
}

public sealed class PackageHistoricalSupplierLeadDto
{
    public long OutcomeRecordId { get; set; }
    public long RawNoticeId { get; set; }
    public long? SupplierId { get; set; }
    public string SupplierName { get; set; } = string.Empty;
    public string OutcomeType { get; set; } = string.Empty;
    public int? Rank { get; set; }
    public decimal? AwardAmount { get; set; }
    public decimal? ProcurementAgencyServiceFeeAmount { get; set; }
    public string Currency { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string NoticeTitle { get; set; } = string.Empty;
    public string SourceUrl { get; set; } = string.Empty;
    public DateTime? PublishTime { get; set; }
    public string PackageNo { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string MatchReason { get; set; } = string.Empty;
    public decimal MatchScore { get; set; }
    public string EvidenceText { get; set; } = string.Empty;
}

public sealed class OutcomeSupplierExtractionResultDto
{
    public long RawNoticeId { get; set; }
    public bool IsOutcomeNotice { get; set; }
    public int ExtractedCount { get; set; }
    public int SavedCount { get; set; }
    public int BuyerCreatedCount { get; set; }
    public int BuyerUpdatedCount { get; set; }
    public int SupplierCreatedCount { get; set; }
    public int SupplierUpdatedCount { get; set; }
    public string Message { get; set; } = string.Empty;
}

public sealed class BidOpsOrganizationMasterDataSyncResult
{
    public int BuyerCreatedCount { get; set; }
    public int BuyerUpdatedCount { get; set; }
    public int SupplierCreatedCount { get; set; }
    public int SupplierUpdatedCount { get; set; }
}

public sealed class OutcomeSupplierBackfillEnqueueDto
{
    public int RequestedMaxItems { get; set; }
    public int QueuedCount { get; set; }
    public List<EnqueueJobDto> Jobs { get; set; } = [];
}

public sealed class SupplierAnalysisBucketDto
{
    public string Code { get; set; } = string.Empty;
    public int Count { get; set; }
    public int SupplierCount { get; set; }
}

public sealed class SupplierAnalysisItemDto
{
    public long SupplierId { get; set; }
    public string SupplierNo { get; set; } = string.Empty;
    public string SupplierName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public decimal? QualityScore { get; set; }
    public int CapabilityCount { get; set; }
    public int EvidenceCount { get; set; }
    public int ValidEvidenceCount { get; set; }
    public int ExpiringEvidenceCount { get; set; }
    public int ExpiredEvidenceCount { get; set; }
    public int MatchResultCount { get; set; }
    public int CandidateMatchCount { get; set; }
    public int CautionMatchCount { get; set; }
    public int NotRecommendedMatchCount { get; set; }
    public int GoDecisionCount { get; set; }
    public int NoGoDecisionCount { get; set; }
    public int HoldDecisionCount { get; set; }
    public int PursuitCount { get; set; }
    public DateTime? LastMatchedAtUtc { get; set; }
    public DateTime? LastDecisionAtUtc { get; set; }
    public DateTime? LastPursuitCreatedAtUtc { get; set; }
    public string RiskFlags { get; set; } = string.Empty;
}

public sealed class SupplierAnalysisSummaryDto
{
    public DateTime GeneratedAtUtc { get; set; }
    public string SupplierSource { get; set; } = "SupplierLibrary";
    public string SupplierSourceDescription { get; set; } = string.Empty;
    public string OutcomeExtractionStatus { get; set; } = string.Empty;
    public int TotalSuppliers { get; set; }
    public int ActiveSuppliers { get; set; }
    public int InactiveSuppliers { get; set; }
    public int BlockedSuppliers { get; set; }
    public int SuppliersWithCapabilities { get; set; }
    public int SuppliersWithEvidence { get; set; }
    public int ExpiringEvidenceDocuments { get; set; }
    public int ExpiredEvidenceDocuments { get; set; }
    public decimal? AverageQualityScore { get; set; }
    public int MatchedSupplierCount { get; set; }
    public int CandidateSupplierCount { get; set; }
    public int GoDecisionCount { get; set; }
    public int PursuitSupplierCount { get; set; }
    public int OutcomeRecordCount { get; set; }
    public int OutcomeSupplierCount { get; set; }
    public int AwardedOutcomeCount { get; set; }
    public int CandidateOutcomeCount { get; set; }
    public int LinkedOutcomeSupplierCount { get; set; }
    public List<SupplierOutcomeStatDto> TopOutcomeSuppliers { get; set; } = [];
    public List<SupplierAnalysisBucketDto> CapabilityCategories { get; set; } = [];
    public List<SupplierAnalysisBucketDto> EvidenceStatuses { get; set; } = [];
    public List<SupplierAnalysisItemDto> TopSuppliers { get; set; } = [];
}

public sealed class SupplierMatchRunDto
{
    public long Id { get; set; }
    public long PackageId { get; set; }
    public long? BackgroundJobId { get; set; }
    public string RunNo { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public long RequestedByUserId { get; set; }
    public string RequestedByUserName { get; set; } = string.Empty;
    public string CriteriaSummary { get; set; } = string.Empty;
    public int MaxSuppliers { get; set; }
    public int SupplierCount { get; set; }
    public int MatchedCount { get; set; }
    public int MissingEvidenceCount { get; set; }
    public DateTime? StartedAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string ErrorMessage { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class MissingEvidenceCheckDto
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public long ResultId { get; set; }
    public long PackageId { get; set; }
    public long SupplierId { get; set; }
    public long? RequirementId { get; set; }
    public long? MatchedEvidenceDocumentId { get; set; }
    public string RequiredEvidenceType { get; set; } = string.Empty;
    public string RequirementText { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
}

public sealed class SupplierMatchResultDto
{
    public long Id { get; set; }
    public long RunId { get; set; }
    public long PackageId { get; set; }
    public long SupplierId { get; set; }
    public string SupplierNameSnapshot { get; set; } = string.Empty;
    public int Rank { get; set; }
    public decimal Score { get; set; }
    public string MatchLevel { get; set; } = string.Empty;
    public string Recommendation { get; set; } = string.Empty;
    public bool CategoryMatched { get; set; }
    public bool RegionMatched { get; set; }
    public int EvidenceMatchedCount { get; set; }
    public int MissingEvidenceCount { get; set; }
    public string RiskFlags { get; set; } = string.Empty;
    public string Explanation { get; set; } = string.Empty;
    public List<MissingEvidenceCheckDto> MissingEvidenceChecks { get; set; } = [];
}

public sealed class SupplierMatchRunDetailDto
{
    public SupplierMatchRunDto Run { get; set; } = new();
    public TenderPackageDto? Package { get; set; }
    public List<RequirementItemDto> Requirements { get; set; } = [];
    public List<SupplierMatchResultDto> Results { get; set; } = [];
}

public sealed class StartSupplierMatchRunResponse
{
    public SupplierMatchRunDto Run { get; set; } = new();
    public EnqueueJobDto Job { get; set; } = new(0, string.Empty, string.Empty, false);
}

public sealed class GoNoGoDecisionDto
{
    public long Id { get; set; }
    public long PackageId { get; set; }
    public long? OpportunityId { get; set; }
    public long? MatchRunId { get; set; }
    public long? SupplierMatchResultId { get; set; }
    public long? SupplierId { get; set; }
    public string Decision { get; set; } = string.Empty;
    public string Reason { get; set; } = string.Empty;
    public string RiskSummary { get; set; } = string.Empty;
    public long DecidedByUserId { get; set; }
    public string DecidedByUserName { get; set; } = string.Empty;
    public DateTime DecidedAtUtc { get; set; }
}

public sealed class PursuitDto
{
    public long Id { get; set; }
    public long NoticeId { get; set; }
    public long PackageId { get; set; }
    public long? OpportunityId { get; set; }
    public long? GoNoGoDecisionId { get; set; }
    public long? SupplierId { get; set; }
    public string SupplierNameSnapshot { get; set; } = string.Empty;
    public string PursuitNo { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string NoticeTitle { get; set; } = string.Empty;
    public string PackageNo { get; set; } = string.Empty;
    public string PackageName { get; set; } = string.Empty;
    public string ProjectName { get; set; } = string.Empty;
    public string ProjectCode { get; set; } = string.Empty;
    public string BuyerName { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Stage { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public decimal? EstimatedAmount { get; set; }
    public DateTime? BidDeadlineAtUtc { get; set; }
    public long? OwnerUserId { get; set; }
    public int ProgressPercent { get; set; }
    public string RiskLevel { get; set; } = string.Empty;
    public int TaskCount { get; set; }
    public int OpenTaskCount { get; set; }
    public int OverdueTaskCount { get; set; }
    public DateTime LastStageChangedAtUtc { get; set; }
    public string Remark { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class PursuitTaskDto
{
    public long Id { get; set; }
    public long PursuitId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TaskType { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int Priority { get; set; }
    public long? OwnerUserId { get; set; }
    public DateTime? DueAtUtc { get; set; }
    public DateTime? CompletedAtUtc { get; set; }
    public string Description { get; set; } = string.Empty;
    public string ResultNote { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}

public sealed class PursuitFollowRecordDto
{
    public long Id { get; set; }
    public long PursuitId { get; set; }
    public string FollowType { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime? NextActionAtUtc { get; set; }
    public long? CreatedByUserId { get; set; }
    public string CreatedByUserName { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public sealed class PursuitDetailDto
{
    public PursuitDto Pursuit { get; set; } = new();
    public TenderPackageDto? Package { get; set; }
    public OpportunityDto? Opportunity { get; set; }
    public List<PursuitTaskDto> Tasks { get; set; } = [];
    public List<PursuitFollowRecordDto> FollowRecords { get; set; } = [];
}

public sealed record EnqueueJobDto(
    long JobId,
    string JobType,
    string Queue,
    bool AlreadyExists);
