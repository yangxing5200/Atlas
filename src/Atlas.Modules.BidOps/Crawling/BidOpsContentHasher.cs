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
            normalized = uri.GetLeftPart(UriPartial.Path).TrimEnd('/').ToLowerInvariant();
            if (!string.IsNullOrWhiteSpace(uri.Query))
                normalized = $"{normalized}?{uri.Query.TrimStart('?')}";
            if (!string.IsNullOrWhiteSpace(uri.Fragment))
                normalized = $"{normalized}#{uri.Fragment.TrimStart('#')}";
        }

        return HashText(normalized);
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
