namespace Atlas.Modules.BidOps.Models;

public abstract record BidOpsTenantJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName);

public sealed record ManualUrlImportJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long? SourceId,
    long? ChannelId,
    string DetailUrl,
    string? Title,
    string? NoticeType,
    string? TextContent,
    bool ForceRefresh,
    string? ProjectCode = null)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record RawAttachmentBackfillJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    int MaxItems,
    bool IncludeAlreadyProcessed,
    bool ForceReparse)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record MockCrawlJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long ChannelId)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record StateGridEcpCrawlJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long ChannelId,
    string Mode = BidOpsCrawlModes.Incremental,
    long? CheckpointId = null,
    int? StartPage = null,
    int? PageSize = null,
    int? MaxPages = null,
    DateTime? RangeStartPublishTime = null,
    DateTime? RangeEndPublishTime = null)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record AttachmentProcessJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long RawNoticeId,
    string? ForceParseRunId = null,
    string? ReviewerPrompt = null,
    string? ProjectCode = null)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record StructuredParseJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long RawNoticeId,
    string? ForceParseRunId = null,
    string? ReviewerPrompt = null,
    string? ProjectCode = null)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record MockAiParseJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long RawNoticeId,
    string? ProjectCode = null)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record OpportunityMaintenanceJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    int MaxItems,
    int DeadlineWarningDays,
    int StaleDays)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record SupplierEvidenceExpiryScanJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    int MaxItems,
    int WarningDays)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record SupplierMatchRunJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long RunId,
    int MaxSuppliers)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record OutcomeSupplierExtractJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long RawNoticeId,
    string? ReviewerPrompt = null,
    string? ProjectCode = null)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record ReviewQualityBackfillJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    int MaxItems,
    string? NoticeType,
    string? RiskLevel,
    bool DryRun,
    long? SourceId,
    bool PauseSourceAware)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);
