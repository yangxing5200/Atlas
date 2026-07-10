using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using Atlas.Core.Entities.Global;

namespace Atlas.BackgroundTasks;

internal sealed record BackgroundJobBusinessLink(
    string? SourceModule,
    string? BusinessType,
    long? BusinessId,
    string? CorrelationId)
{
    public bool HasAnyValue =>
        !string.IsNullOrWhiteSpace(SourceModule) ||
        !string.IsNullOrWhiteSpace(BusinessType) ||
        BusinessId.HasValue ||
        !string.IsNullOrWhiteSpace(CorrelationId);
}

internal static class BackgroundJobBusinessLinkInference
{
    public static BackgroundJobBusinessLink Resolve(
        string jobType,
        string queue,
        string payloadJson,
        string? deduplicationKey,
        string? explicitSourceModule,
        string? explicitBusinessType,
        long? explicitBusinessId,
        string? explicitCorrelationId)
    {
        return ResolveCore(
            jobType,
            queue,
            payloadJson,
            result: null,
            deduplicationKey,
            explicitSourceModule,
            explicitBusinessType,
            explicitBusinessId,
            explicitCorrelationId,
            includeResult: false);
    }

    public static BackgroundJobBusinessLink Infer(
        string jobType,
        string queue,
        string payloadJson,
        string? result,
        string? deduplicationKey,
        string? existingSourceModule = null,
        string? existingBusinessType = null,
        long? existingBusinessId = null,
        string? existingCorrelationId = null,
        bool includeResult = true)
    {
        return ResolveCore(
            jobType,
            queue,
            payloadJson,
            result,
            deduplicationKey,
            existingSourceModule,
            existingBusinessType,
            existingBusinessId,
            existingCorrelationId,
            includeResult);
    }

    private static BackgroundJobBusinessLink ResolveCore(
        string jobType,
        string queue,
        string payloadJson,
        string? result,
        string? deduplicationKey,
        string? sourceModule,
        string? businessType,
        long? businessId,
        string? correlationId,
        bool includeResult)
    {
        var normalizedSourceModule = Normalize(sourceModule, BackgroundJobBusinessConstants.SourceModuleMaxLength);
        var normalizedBusinessType = Normalize(businessType, BackgroundJobBusinessConstants.BusinessTypeMaxLength);
        var normalizedBusinessId = businessId is > 0 ? businessId : null;
        var normalizedCorrelationId = Normalize(correlationId, BackgroundJobBusinessConstants.CorrelationIdMaxLength);

        if (string.IsNullOrWhiteSpace(normalizedSourceModule) && IsBidOpsJob(jobType, queue))
            normalizedSourceModule = BackgroundJobBusinessConstants.BidOpsSourceModule;

        if (IsBidOpsSource(normalizedSourceModule))
        {
            var rawNoticeId = normalizedBusinessId ??
                              ExtractRawNoticeIdFromJson(payloadJson) ??
                              ExtractRawNoticeIdFromKnownDeduplicationKey(deduplicationKey);
            if (!rawNoticeId.HasValue && includeResult)
                rawNoticeId = ExtractRawNoticeIdFromText(result);

            if (rawNoticeId is > 0)
            {
                normalizedBusinessType = string.IsNullOrWhiteSpace(normalizedBusinessType)
                    ? BackgroundJobBusinessConstants.RawNoticeBusinessType
                    : normalizedBusinessType;
                normalizedBusinessId = rawNoticeId.Value;
                normalizedCorrelationId ??= rawNoticeId.Value.ToString(CultureInfo.InvariantCulture);
            }
        }

        return new BackgroundJobBusinessLink(
            normalizedSourceModule,
            normalizedBusinessType,
            normalizedBusinessId,
            normalizedCorrelationId);
    }

    private static bool IsBidOpsJob(string jobType, string queue)
    {
        return string.Equals(queue, "bidops", StringComparison.OrdinalIgnoreCase) ||
               jobType.StartsWith("bidops.", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBidOpsSource(string? sourceModule)
    {
        return string.Equals(
            sourceModule,
            BackgroundJobBusinessConstants.BidOpsSourceModule,
            StringComparison.OrdinalIgnoreCase);
    }

    private static long? ExtractRawNoticeIdFromText(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        return ExtractRawNoticeIdFromJson(text) ?? ExtractRawNoticeIdFromAssignment(text);
    }

    private static long? ExtractRawNoticeIdFromJson(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        try
        {
            using var document = JsonDocument.Parse(text);
            return FindRawNoticeId(document.RootElement);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static long? FindRawNoticeId(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Object)
        {
            foreach (var property in element.EnumerateObject())
            {
                if (property.NameEquals("rawNoticeId") || property.NameEquals("RawNoticeId"))
                {
                    var value = ReadPositiveLong(property.Value);
                    if (value.HasValue)
                        return value.Value;
                }

                var nested = FindRawNoticeId(property.Value);
                if (nested.HasValue)
                    return nested.Value;
            }
        }
        else if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                var nested = FindRawNoticeId(item);
                if (nested.HasValue)
                    return nested.Value;
            }
        }

        return null;
    }

    private static long? ReadPositiveLong(JsonElement element)
    {
        if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var number) && number > 0)
            return number;

        if (element.ValueKind == JsonValueKind.String &&
            long.TryParse(element.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var textNumber) &&
            textNumber > 0)
        {
            return textNumber;
        }

        return null;
    }

    private static long? ExtractRawNoticeIdFromAssignment(string text)
    {
        var match = Regex.Match(
            text,
            @"(?:^|[^\w])(?:rawNoticeId|RawNoticeId)\s*=\s*(?<value>\d+)(?:$|[^\d])",
            RegexOptions.CultureInvariant);
        return match.Success &&
               long.TryParse(match.Groups["value"].Value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) &&
               value > 0
            ? value
            : null;
    }

    private static long? ExtractRawNoticeIdFromKnownDeduplicationKey(string? deduplicationKey)
    {
        if (string.IsNullOrWhiteSpace(deduplicationKey))
            return null;

        var parts = deduplicationKey
            .Split(':', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length < 2 || !string.Equals(parts[0], "bidops", StringComparison.OrdinalIgnoreCase))
            return null;

        var keyType = parts[1];
        if ((string.Equals(keyType, "attachment-process", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(keyType, "structured-parse", StringComparison.OrdinalIgnoreCase)) &&
            parts.Length > 4 &&
            TryParsePositiveLong(parts[4], out var versionedRawNoticeId))
        {
            return versionedRawNoticeId;
        }

        if ((string.Equals(keyType, "outcome-supplier-extract", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(keyType, "review-outcome-ai-reparse", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(keyType, "manual-reparse", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(keyType, "approval-outcome-supplier-extract", StringComparison.OrdinalIgnoreCase) ||
             string.Equals(keyType, "lifecycle-outcome-supplier-reparse", StringComparison.OrdinalIgnoreCase)) &&
            parts.Length > 3 &&
            TryParsePositiveLong(parts[3], out var rawNoticeId))
        {
            return rawNoticeId;
        }

        for (var i = 2; i < parts.Length - 1; i++)
        {
            if (string.Equals(parts[i], "raw", StringComparison.OrdinalIgnoreCase) &&
                TryParsePositiveLong(parts[i + 1], out var rawTokenId))
            {
                return rawTokenId;
            }
        }

        return null;
    }

    private static bool TryParsePositiveLong(string value, out long result)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out result) &&
               result > 0;
    }

    private static string? Normalize(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        var trimmed = value.Trim();
        return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
    }
}
