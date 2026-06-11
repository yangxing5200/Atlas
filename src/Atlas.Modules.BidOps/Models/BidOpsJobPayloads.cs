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
    string? TextContent)
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
    long ChannelId)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record AttachmentProcessJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long RawNoticeId)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record StructuredParseJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long RawNoticeId)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);

public sealed record MockAiParseJobPayload(
    long TenantId,
    long? StoreId,
    long UserId,
    string UserName,
    long RawNoticeId)
    : BidOpsTenantJobPayload(TenantId, StoreId, UserId, UserName);
