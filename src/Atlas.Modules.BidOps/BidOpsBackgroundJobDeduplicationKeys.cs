using Atlas.Modules.BidOps.Entities.Crawling;

namespace Atlas.Modules.BidOps;

internal static class BidOpsBackgroundJobDeduplicationKeys
{
    private const int MaxDeduplicationKeyLength = 300;

    public static string AttachmentProcess(long tenantId, long rawNoticeId, string? contentHash)
    {
        return $"bidops:attachment-process:{BidOpsSystemValues.StructuredParserVersion}:{tenantId}:{rawNoticeId}:{NormalizePart(contentHash, "no-content-hash")}";
    }

    public static string ScheduledScan(
        string sourceType,
        long tenantId,
        CrawlChannel channel,
        CrawlCheckpoint? checkpoint)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var mode = checkpoint?.Mode ?? BidOpsCrawlModes.Incremental;
        if (checkpoint != null)
        {
            var checkpointStamp = FormatStamp(checkpoint.UpdatedAt ?? checkpoint.LastRunAt ?? checkpoint.CreatedAt);
            return $"bidops:scheduled:{NormalizePart(sourceType, "source")}:{NormalizePart(mode, "mode")}:{tenantId}:{channel.Id}:checkpoint:{checkpoint.Id}:cursor:{NormalizePart(checkpoint.NextCursor, "1")}:state:{checkpointStamp}";
        }

        var channelStamp = FormatStamp(channel.LastScanTime ?? channel.UpdatedAt ?? channel.CreatedAt);
        return $"bidops:scheduled:{NormalizePart(sourceType, "source")}:{NormalizePart(mode, "mode")}:{tenantId}:{channel.Id}:last-scan:{channelStamp}";
    }

    public static string ManualScan(
        string sourceType,
        long tenantId,
        CrawlChannel channel)
    {
        ArgumentNullException.ThrowIfNull(channel);

        var channelStamp = FormatStamp(channel.LastScanTime ?? channel.UpdatedAt ?? channel.CreatedAt);
        return $"bidops:manual-scan:{NormalizePart(sourceType, "source")}:{tenantId}:{channel.Id}:last-scan:{channelStamp}";
    }

    public static string CheckpointScan(
        string sourceType,
        long tenantId,
        CrawlChannel channel,
        CrawlCheckpoint checkpoint)
    {
        return ScheduledScan(sourceType, tenantId, channel, checkpoint).Replace(
            "bidops:scheduled:",
            "bidops:checkpoint-scan:",
            StringComparison.Ordinal);
    }

    public static string LifecycleReverseClosure(
        long tenantId,
        long? rawNoticeId,
        string? awardUrl,
        bool persistLinks,
        string? runId = null)
    {
        var baseKey = $"bidops:lifecycle:reverse-closure:{tenantId}:raw:{rawNoticeId?.ToString() ?? "none"}:url:{NormalizePart(awardUrl, "none")}:persist:{persistLinks}";
        if (string.IsNullOrWhiteSpace(runId))
            return baseKey;

        var suffix = $":run:{NormalizePart(runId, "manual")}";
        var maxBaseLength = Math.Max(0, MaxDeduplicationKeyLength - suffix.Length);
        return baseKey.Length <= maxBaseLength
            ? baseKey + suffix
            : baseKey[..maxBaseLength] + suffix;
    }

    public static string LifecycleFieldEnrichment(
        long tenantId,
        long linkId,
        string? reviewerPrompt)
    {
        return $"bidops:lifecycle:field-enrichment:{tenantId}:link:{linkId}:prompt:{NormalizePart(reviewerPrompt, "auto")}";
    }

    private static string FormatStamp(DateTime value)
    {
        return value == default
            ? "never"
            : value.ToString("yyyyMMddHHmmssffff");
    }

    private static string NormalizePart(string? value, string fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
            return fallback;

        var trimmed = value.Trim();
        Span<char> buffer = stackalloc char[trimmed.Length];
        var length = 0;
        foreach (var ch in trimmed)
        {
            buffer[length++] = char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.'
                ? ch
                : '_';
        }

        return new string(buffer[..length]);
    }
}
