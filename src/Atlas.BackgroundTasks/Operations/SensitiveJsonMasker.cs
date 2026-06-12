using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;

namespace Atlas.BackgroundTasks.Operations;

public interface ISensitiveJsonMasker
{
    string MaskJson(string? json);
    string MaskText(string? text, int maxCharacters = 2_000);
}

public sealed class SensitiveJsonMasker : ISensitiveJsonMasker
{
    private const string MaskValue = "***";
    private const int DefaultMaxCharacters = 2_000;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = true
    };

    private static readonly string[] SensitiveKeyFragments =
    [
        "password",
        "pwd",
        "secret",
        "token",
        "accessToken",
        "refreshToken",
        "apiKey",
        "apikey",
        "authorization",
        "cookie",
        "set-cookie",
        "phone",
        "mobile",
        "email",
        "idCard",
        "bankCard"
    ];

    private static readonly Regex SensitiveAssignmentRegex = new(
        @"(?i)\b(password|pwd|secret|token|accessToken|refreshToken|apiKey|apikey|authorization|cookie|set-cookie|phone|mobile|email|idCard|bankCard)\b\s*[:=]\s*[^,\s;]+",
        RegexOptions.Compiled);

    public string MaskJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return string.Empty;

        var trimmed = json.Trim();
        try
        {
            var node = JsonNode.Parse(trimmed);
            if (node == null)
                return string.Empty;

            MaskNode(node);
            return TrimToMax(node.ToJsonString(JsonOptions), DefaultMaxCharacters * 5);
        }
        catch (JsonException)
        {
            return MaskText(trimmed);
        }
    }

    public string MaskText(string? text, int maxCharacters = DefaultMaxCharacters)
    {
        if (string.IsNullOrWhiteSpace(text))
            return string.Empty;

        var masked = SensitiveAssignmentRegex.Replace(text.Trim(), match =>
        {
            var separatorIndex = match.Value.IndexOf(':');
            if (separatorIndex < 0)
                separatorIndex = match.Value.IndexOf('=');

            return separatorIndex < 0
                ? MaskValue
                : string.Concat(match.Value.AsSpan(0, separatorIndex + 1), MaskValue);
        });

        return TrimToMax(masked, maxCharacters);
    }

    private static void MaskNode(JsonNode node)
    {
        if (node is JsonObject obj)
        {
            foreach (var key in obj.Select(x => x.Key).ToArray())
            {
                if (IsSensitiveKey(key))
                {
                    obj[key] = MaskValue;
                    continue;
                }

                var child = obj[key];
                if (child != null)
                    MaskNode(child);
            }

            return;
        }

        if (node is JsonArray array)
        {
            foreach (var child in array)
            {
                if (child != null)
                    MaskNode(child);
            }

            return;
        }

        if (node is JsonValue value &&
            value.TryGetValue<string>(out var text) &&
            text.Length > DefaultMaxCharacters)
        {
            node.ReplaceWith(TrimToMax(text, DefaultMaxCharacters));
        }
    }

    private static bool IsSensitiveKey(string key)
    {
        return SensitiveKeyFragments.Any(fragment =>
            key.Contains(fragment, StringComparison.OrdinalIgnoreCase));
    }

    private static string TrimToMax(string value, int maxCharacters)
    {
        if (value.Length <= maxCharacters)
            return value;

        return value[..maxCharacters] + "...";
    }
}
