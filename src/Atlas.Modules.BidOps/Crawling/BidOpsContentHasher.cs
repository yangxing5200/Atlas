using System.Security.Cryptography;
using System.Text;

namespace Atlas.Modules.BidOps.Crawling;

public sealed class BidOpsContentHasher
{
    public string HashText(string? value)
    {
        var normalized = NormalizeText(value);
        return HashBytes(Encoding.UTF8.GetBytes(normalized));
    }

    public string HashUrl(string value)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(value);

        var normalized = value.Trim();
        if (Uri.TryCreate(normalized, UriKind.Absolute, out var uri))
        {
            normalized = NormalizeAbsoluteUrl(uri);
        }

        return HashText(normalized);
    }

    private static string NormalizeAbsoluteUrl(Uri uri)
    {
        var normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
        if (uri.AbsolutePath.EndsWith("/downLoadWinFile", StringComparison.OrdinalIgnoreCase))
        {
            var fileName = GetQueryParameter(uri.Query, "fileName");
            if (!string.IsNullOrWhiteSpace(fileName))
                return $"{normalized}?filename={fileName.Trim()}";
        }

        if (!string.IsNullOrWhiteSpace(uri.Query))
            normalized = $"{normalized}?{uri.Query.TrimStart('?')}";
        if (!string.IsNullOrWhiteSpace(uri.Fragment))
            normalized = $"{normalized}#{uri.Fragment.TrimStart('#')}";

        return normalized;
    }

    private static string GetQueryParameter(string query, string name)
    {
        if (string.IsNullOrWhiteSpace(query))
            return string.Empty;

        foreach (var part in query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries))
        {
            var separatorIndex = part.IndexOf('=', StringComparison.Ordinal);
            var key = separatorIndex < 0 ? part : part[..separatorIndex];
            if (!string.Equals(Uri.UnescapeDataString(key), name, StringComparison.OrdinalIgnoreCase))
                continue;

            var value = separatorIndex < 0 ? string.Empty : part[(separatorIndex + 1)..];
            return Uri.UnescapeDataString(value.Replace("+", " ", StringComparison.Ordinal));
        }

        return string.Empty;
    }

    private static string NormalizeText(string? value)
    {
        return string.Join(' ', (value ?? string.Empty).Split(
            Array.Empty<char>(),
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries));
    }

    private static string HashBytes(byte[] bytes)
    {
        return Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
    }
}
