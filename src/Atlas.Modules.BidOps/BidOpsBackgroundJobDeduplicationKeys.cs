using Atlas.Modules.BidOps.Entities.Crawling;

namespace Atlas.Modules.BidOps;

internal static class BidOpsBackgroundJobDeduplicationKeys
{
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
