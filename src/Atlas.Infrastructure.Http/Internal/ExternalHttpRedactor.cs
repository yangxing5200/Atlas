using System.Text.RegularExpressions;
using Atlas.Infrastructure.Http.Configuration;

namespace Atlas.Infrastructure.Http.Internal;

public static partial class ExternalHttpRedactor
{
    private const string Mask = "***";

    public static string RedactText(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var redacted = SensitiveAssignmentRegex().Replace(value, match =>
        {
            var separatorIndex = match.Value.IndexOfAny([':', '=']);
            return separatorIndex < 0
                ? Mask
                : string.Concat(match.Value.AsSpan(0, separatorIndex + 1), Mask);
        });

        return BearerRegex().Replace(redacted, $"Bearer {Mask}");
    }

    public static string RedactUri(Uri? uri)
    {
        if (uri is null)
            return string.Empty;

        if (string.IsNullOrWhiteSpace(uri.Query))
            return uri.ToString();

        var query = uri.Query.TrimStart('?').Split('&', StringSplitOptions.RemoveEmptyEntries);
        var redactedQuery = query.Select(part =>
        {
            var separatorIndex = part.IndexOf('=');
            if (separatorIndex <= 0)
                return part;

            var key = Uri.UnescapeDataString(part[..separatorIndex]);
            return IsSensitiveName(key)
                ? string.Concat(part.AsSpan(0, separatorIndex + 1), Mask)
                : part;
        });

        var builder = new UriBuilder(uri)
        {
            Query = string.Join("&", redactedQuery)
        };

        return builder.Uri.ToString();
    }

    public static IReadOnlyDictionary<string, string> RedactHeaders(
        IEnumerable<KeyValuePair<string, IEnumerable<string>>> headers,
        ResolvedExternalHttpLoggingOptions options)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var header in headers)
        {
            result[header.Key] = IsSensitiveHeader(header.Key, options)
                ? Mask
                : RedactText(string.Join(",", header.Value));
        }

        return result;
    }

    public static bool IsSensitiveHeader(string headerName, ResolvedExternalHttpLoggingOptions options)
    {
        return options.SensitiveHeaders.Contains(headerName) || IsSensitiveName(headerName);
    }

    private static bool IsSensitiveName(string name)
    {
        return name.Contains("password", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("passwd", StringComparison.OrdinalIgnoreCase) ||
            name.Equals("pwd", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("token", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("secret", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("api-key", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("apikey", StringComparison.OrdinalIgnoreCase) ||
            name.Contains("authorization", StringComparison.OrdinalIgnoreCase);
    }

    [GeneratedRegex(@"(?i)(password|passwd|pwd|token|secret|api[-_]?key)\s*[:=]\s*[^,\s;}]+", RegexOptions.Compiled)]
    private static partial Regex SensitiveAssignmentRegex();

    [GeneratedRegex(@"(?i)Bearer\s+[A-Za-z0-9._~+/=-]+", RegexOptions.Compiled)]
    private static partial Regex BearerRegex();
}
