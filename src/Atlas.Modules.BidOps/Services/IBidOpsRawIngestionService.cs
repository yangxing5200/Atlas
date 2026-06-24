namespace Atlas.Modules.BidOps.Services;

public sealed record RawAttachmentCandidate(
    string FileName,
    string FileUrl,
    string FileType,
    long? FileSize);

public sealed record RawIngestionCommand(
    long? SourceId,
    long? ChannelId,
    string DetailUrl,
    string Title,
    string NoticeType,
    string TextContent,
    string HtmlContent,
    DateTime? PublishTime,
    IReadOnlyList<RawAttachmentCandidate>? Attachments = null,
    bool ForceRefresh = false);

public sealed record RawIngestionResult(
    long RawNoticeId,
    string Status,
    bool ShouldProcess);

public interface IBidOpsRawIngestionService
{
    Task<long> ImportManualUrlAsync(
        RawIngestionCommand command,
        long? backgroundJobId,
        CancellationToken ct = default);

    Task<long> IngestPublicNoticeAsync(
        RawIngestionCommand command,
        long? backgroundJobId,
        string operation,
        CancellationToken ct = default);

    Task<RawIngestionResult> IngestPublicNoticeWithResultAsync(
        RawIngestionCommand command,
        long? backgroundJobId,
        string operation,
        CancellationToken ct = default);

    Task<long?> FindExistingRawNoticeIdByUrlAsync(
        string noticeType,
        string detailUrl,
        CancellationToken ct = default);

    Task<long> CreateMockRawNoticeAsync(
        long channelId,
        long? backgroundJobId,
        CancellationToken ct = default);
}
