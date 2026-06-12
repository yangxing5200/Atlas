using Atlas.Core.Exceptions;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsAttachmentProcessingService : IBidOpsAttachmentProcessingService
{
    private const int DefaultMaxAttachmentBytes = 30 * 1024 * 1024;

    private readonly HttpClient _httpClient;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _attachments;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBidOpsFileStore _fileStore;
    private readonly IBidOpsTextExtractor _textExtractor;
    private readonly ILogger<BidOpsAttachmentProcessingService> _logger;
    private readonly int _maxAttachmentBytes;

    public BidOpsAttachmentProcessingService(
        HttpClient httpClient,
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> attachments,
        IUnitOfWork unitOfWork,
        IBidOpsFileStore fileStore,
        IBidOpsTextExtractor textExtractor,
        IConfiguration configuration,
        ILogger<BidOpsAttachmentProcessingService> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _attachments = attachments ?? throw new ArgumentNullException(nameof(attachments));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _textExtractor = textExtractor ?? throw new ArgumentNullException(nameof(textExtractor));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _maxAttachmentBytes = Math.Clamp(
            configuration.GetValue<int?>("BidOps:Attachments:MaxAttachmentBytes") ?? DefaultMaxAttachmentBytes,
            128 * 1024,
            100 * 1024 * 1024);
    }

    public async Task<BidOpsAttachmentProcessingResult> ProcessRawNoticeAttachmentsAsync(
        long rawNoticeId,
        CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct)
            ?? throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");

        var query = await _attachments.QueryTrackingAsync(ct);
        var attachments = await query
            .Where(x => x.RawNoticeId == raw.Id)
            .ToListAsync(ct);

        var downloaded = 0;
        var extracted = 0;
        var failed = 0;

        foreach (var attachment in attachments)
        {
            try
            {
                if (attachment.DownloadStatus != DownloadStatus.Succeeded)
                {
                    await DownloadAsync(raw, attachment, attachments, ct);
                    downloaded++;
                }

                if (attachment.DownloadStatus == DownloadStatus.Succeeded &&
                    attachment.TextExtractStatus != TextExtractStatus.Succeeded &&
                    !string.IsNullOrWhiteSpace(attachment.StorageKey))
                {
                    if (await ExtractTextAsync(attachment, ct))
                        extracted++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                failed++;
                attachment.DownloadStatus = attachment.DownloadStatus == DownloadStatus.Succeeded
                    ? attachment.DownloadStatus
                    : DownloadStatus.Failed;
                attachment.TextExtractStatus = attachment.DownloadStatus == DownloadStatus.Succeeded
                    ? TextExtractStatus.Failed
                    : attachment.TextExtractStatus;
                _logger.LogWarning(
                    ex,
                    "BidOps attachment processing failed for raw notice {RawNoticeId}, attachment {AttachmentId}.",
                    raw.Id,
                    attachment.Id);
            }
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return new BidOpsAttachmentProcessingResult(raw.Id, attachments.Count, downloaded, extracted, failed);
    }

    private async Task DownloadAsync(
        RawNotice raw,
        RawAttachment attachment,
        IReadOnlyCollection<RawAttachment> rawAttachments,
        CancellationToken ct)
    {
        if (!Uri.TryCreate(attachment.FileUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme is not ("http" or "https"))
        {
            attachment.DownloadStatus = DownloadStatus.Skipped;
            attachment.TextExtractStatus = TextExtractStatus.Skipped;
            return;
        }

        EnsureAttachmentAllowed(raw, uri);

        using var request = new HttpRequestMessage(HttpMethod.Get, uri);
        request.Headers.UserAgent.ParseAdd("AtlasBidOps/0.1 (+public procurement attachment downloader; no-login)");

        using var response = await _httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, ct);
        response.EnsureSuccessStatusCode();

        var contentLength = response.Content.Headers.ContentLength;
        if (contentLength.HasValue && contentLength.Value > _maxAttachmentBytes)
            throw new AtlasException($"BidOps attachment exceeds max size: {contentLength.Value} bytes.");

        await using var source = await response.Content.ReadAsStreamAsync(ct);
        await using var capped = await CopyWithLimitAsync(source, _maxAttachmentBytes, ct);
        capped.Position = 0;

        var contentType = response.Content.Headers.ContentType?.MediaType ?? GuessContentType(attachment.FileName, attachment.FileType);
        var stored = await _fileStore.SaveAsync(capped, attachment.FileName, contentType, ct);
        attachment.StorageProvider = stored.StorageProvider;
        attachment.StorageKey = stored.StorageKey;
        attachment.FileSize = stored.FileSize;
        attachment.FileHash = ResolveDownloadedFileHash(raw.Id, attachment, stored.FileHash, rawAttachments);
        attachment.FileType = NormalizeFileType(attachment.FileType, stored.FileName, contentType);
        attachment.DownloadStatus = DownloadStatus.Succeeded;
    }

    private string ResolveDownloadedFileHash(
        long rawNoticeId,
        RawAttachment attachment,
        string downloadedFileHash,
        IReadOnlyCollection<RawAttachment> rawAttachments)
    {
        if (string.IsNullOrWhiteSpace(downloadedFileHash))
            return attachment.FileHash;

        var hasCollision = rawAttachments.Any(x =>
            x.Id != attachment.Id &&
            string.Equals(x.FileHash, downloadedFileHash, StringComparison.OrdinalIgnoreCase));
        if (!hasCollision)
            return downloadedFileHash;

        _logger.LogInformation(
            "BidOps attachment {AttachmentId} for raw notice {RawNoticeId} downloaded duplicate content hash {FileHash}; keeping the existing attachment identity hash.",
            attachment.Id,
            rawNoticeId,
            downloadedFileHash);
        return attachment.FileHash;
    }

    private async Task<bool> ExtractTextAsync(
        RawAttachment attachment,
        CancellationToken ct)
    {
        await using var file = await _fileStore.OpenReadAsync(attachment.StorageKey, ct);
        var text = await _textExtractor.ExtractAsync(
            file,
            attachment.FileName,
            GuessContentType(attachment.FileName, attachment.FileType),
            ct);
        if (string.IsNullOrWhiteSpace(text))
        {
            attachment.TextExtractStatus = TextExtractStatus.Skipped;
            return false;
        }

        await using var textStream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes(text));
        var stored = await _fileStore.SaveAsync(textStream, $"{Path.GetFileNameWithoutExtension(attachment.FileName)}.extracted.txt", "text/plain", ct);
        attachment.TextContentStorageKey = stored.StorageKey;
        attachment.TextExtractStatus = TextExtractStatus.Succeeded;
        return true;
    }

    private static async Task<MemoryStream> CopyWithLimitAsync(
        Stream source,
        int maxBytes,
        CancellationToken ct)
    {
        var target = new MemoryStream();
        var buffer = new byte[81920];
        var total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, ct)) > 0)
        {
            total += read;
            if (total > maxBytes)
                throw new AtlasException($"BidOps attachment exceeds max size: {maxBytes} bytes.");

            await target.WriteAsync(buffer.AsMemory(0, read), ct);
        }

        return target;
    }

    private static void EnsureAttachmentAllowed(
        RawNotice raw,
        Uri uri)
    {
        if (!Uri.TryCreate(raw.DetailUrl, UriKind.Absolute, out var detailUri))
            return;

        if (!string.Equals(uri.Host, detailUri.Host, StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.EndsWith(".sgcc.com.cn", StringComparison.OrdinalIgnoreCase) &&
            !uri.Host.EndsWith("sgcc.com.cn", StringComparison.OrdinalIgnoreCase))
        {
            throw new AtlasException($"BidOps rejected attachment from non-source host: {uri.Host}");
        }
    }

    private static string GuessContentType(string fileName, string fileType)
    {
        var extension = NormalizeFileType(fileType, fileName, string.Empty);
        return extension switch
        {
            "pdf" => "application/pdf",
            "docx" => "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
            "doc" => "application/msword",
            "html" or "htm" => "text/html",
            "txt" => "text/plain",
            _ => "application/octet-stream"
        };
    }

    private static string NormalizeFileType(string fileType, string fileName, string contentType)
    {
        if (!string.IsNullOrWhiteSpace(fileType) && fileType != "file")
            return fileType.Trim().TrimStart('.').ToLowerInvariant();

        var extension = Path.GetExtension(fileName).TrimStart('.').ToLowerInvariant();
        if (!string.IsNullOrWhiteSpace(extension))
            return extension;

        if (contentType.Contains("pdf", StringComparison.OrdinalIgnoreCase))
            return "pdf";
        if (contentType.Contains("wordprocessingml", StringComparison.OrdinalIgnoreCase))
            return "docx";
        if (contentType.Contains("msword", StringComparison.OrdinalIgnoreCase))
            return "doc";
        if (contentType.Contains("html", StringComparison.OrdinalIgnoreCase))
            return "html";
        if (contentType.Contains("text", StringComparison.OrdinalIgnoreCase))
            return "txt";

        return "file";
    }
}
