using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Entities.Crawling;

namespace Atlas.Modules.BidOps.Models;

internal static class BidOpsJobProjectCode
{
    private const int MaxSearchCharacters = 80_000;

    public static string FromRawNotice(RawNotice? raw)
    {
        return raw == null
            ? string.Empty
            : FromText(raw.Title, raw.TextPreview);
    }

    public static string FromText(params string?[] values)
    {
        foreach (var value in values)
        {
            if (string.IsNullOrWhiteSpace(value))
                continue;

            var text = value.Length <= MaxSearchCharacters
                ? value
                : value[..MaxSearchCharacters];
            var projectCode = BidOpsEvidenceText.ExtractProjectCode(text);
            if (!string.IsNullOrWhiteSpace(projectCode))
                return projectCode.Trim();
        }

        return string.Empty;
    }

    public static string FirstMeaningful(params string?[] values)
    {
        foreach (var value in values)
        {
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return string.Empty;
    }
}
