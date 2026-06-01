using System.Text.RegularExpressions;
using Serilog.Events;

namespace Atlas.Infrastructure.Logging.Policies;

public static class SensitiveDataMasker
{
    public const string SecretMask = "***REDACTED***";

    private static readonly Regex EmailPattern = new(
        @"(?<local>[A-Z0-9._%+-]{1,64})@(?<domain>[A-Z0-9.-]+\.[A-Z]{2,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhonePattern = new(
        @"(?<!\d)(?<prefix>1[3-9]\d)(?<middle>\d{4})(?<suffix>\d{4})(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex[] SecretPatterns =
    [
        new(@"(?i)(password|passwd|pwd|token|secret|api[-_]?key)\s*[:=]\s*[^,\s;}]+", RegexOptions.Compiled),
        new(@"\b\d{15,19}\b", RegexOptions.Compiled),
        new(@"\b\d{17}[\dXx]\b", RegexOptions.Compiled)
    ];

    public static LogEventPropertyValue MaskValue(
        string propertyName,
        LogEventPropertyValue value,
        ISet<string> sensitiveFields)
    {
        if (IsSensitiveField(propertyName, sensitiveFields))
            return new ScalarValue(MaskByField(propertyName, value));

        return value switch
        {
            ScalarValue { Value: string text } => new ScalarValue(MaskText(text)),
            StructureValue structure => new StructureValue(
                structure.Properties
                    .Select(property => new LogEventProperty(
                        property.Name,
                        MaskValue(property.Name, property.Value, sensitiveFields)))
                    .ToArray(),
                structure.TypeTag),
            SequenceValue sequence => new SequenceValue(
                sequence.Elements.Select(element => MaskValue(propertyName, element, sensitiveFields)).ToArray()),
            DictionaryValue dictionary => new DictionaryValue(
                dictionary.Elements.Select(pair => new KeyValuePair<ScalarValue, LogEventPropertyValue>(
                    pair.Key,
                    MaskValue(propertyName, pair.Value, sensitiveFields))).ToArray()),
            _ => value
        };
    }

    public static string MaskText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var masked = EmailPattern.Replace(value, match => MaskEmail(match.Value));
        masked = PhonePattern.Replace(masked, match => $"{match.Groups["prefix"].Value}****{match.Groups["suffix"].Value}");

        foreach (var pattern in SecretPatterns)
        {
            masked = pattern.Replace(masked, match =>
            {
                var separatorIndex = match.Value.IndexOfAny([':', '=']);
                return separatorIndex > 0
                    ? $"{match.Value[..(separatorIndex + 1)]}{SecretMask}"
                    : SecretMask;
            });
        }

        return masked;
    }

    public static string MaskByField(string fieldName, object? value)
    {
        var text = value switch
        {
            ScalarValue scalar => scalar.Value?.ToString() ?? string.Empty,
            LogEventPropertyValue propertyValue => propertyValue.ToString(),
            _ => value?.ToString() ?? string.Empty
        };

        if (fieldName.Contains("email", StringComparison.OrdinalIgnoreCase))
            return MaskEmail(text);

        if (fieldName.Contains("phone", StringComparison.OrdinalIgnoreCase) ||
            fieldName.Contains("mobile", StringComparison.OrdinalIgnoreCase))
            return PhonePattern.Replace(text, match => $"{match.Groups["prefix"].Value}****{match.Groups["suffix"].Value}");

        return SecretMask;
    }

    private static bool IsSensitiveField(string fieldName, ISet<string> sensitiveFields)
    {
        return sensitiveFields.Contains(fieldName);
    }

    private static string MaskEmail(string value)
    {
        var atIndex = value.IndexOf('@');
        if (atIndex <= 0)
            return SecretMask;

        var local = value[..atIndex];
        var domain = value[atIndex..];
        var prefix = local.Length <= 1 ? local : local[..1];
        return $"{prefix}***{domain}";
    }
}
