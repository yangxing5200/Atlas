using System.Text.RegularExpressions;

namespace Atlas.Core.DataMasking;

public sealed class SensitiveValueMasker : ISensitiveValueMasker
{
    public const string SecretMask = "***REDACTED***";

    private static readonly Regex EmailPattern = new(
        @"(?<local>[A-Z0-9._%+-]{1,64})@(?<domain>[A-Z0-9.-]+\.[A-Z]{2,})",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex PhonePattern = new(
        @"(?<!\d)(?<prefix>1[3-9]\d)(?<middle>\d{4})(?<suffix>\d{4})(?!\d)",
        RegexOptions.Compiled);

    private static readonly Regex IdCardPattern = new(
        @"\b(?<prefix>\d{6})\d{8}(?<suffix>\d{3}[\dXx])\b",
        RegexOptions.Compiled);

    private static readonly Regex BankCardPattern = new(
        @"\b(?<prefix>\d{4})\d{7,11}(?<suffix>\d{4})\b",
        RegexOptions.Compiled);

    private static readonly Regex Ipv4Pattern = new(
        @"\b(?<a>\d{1,3})\.(?<b>\d{1,3})\.(?<c>\d{1,3})\.(?<d>\d{1,3})\b",
        RegexOptions.Compiled);

    private static readonly Regex[] SecretPatterns =
    [
        new(@"(?i)(password|passwd|pwd|token|secret|api[-_]?key)\s*[:=]\s*[^,\s;}]+", RegexOptions.Compiled),
        new(@"\b\d{15,19}\b", RegexOptions.Compiled),
        new(@"\b\d{17}[\dXx]\b", RegexOptions.Compiled)
    ];

    public string? Mask(string? value, MaskKind kind)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        return kind switch
        {
            MaskKind.Email => MaskEmail(value),
            MaskKind.Phone => MaskPhone(value),
            MaskKind.IdCard => MaskIdCard(value),
            MaskKind.BankCard => MaskBankCard(value),
            MaskKind.IpAddress => MaskIpAddress(value),
            MaskKind.Token or MaskKind.Secret => SecretMask,
            MaskKind.Name => MaskName(value),
            MaskKind.Address => MaskAddress(value),
            _ => SecretMask
        };
    }

    public string MaskText(string value)
    {
        if (string.IsNullOrEmpty(value))
            return value;

        var masked = EmailPattern.Replace(value, match => MaskEmail(match.Value));
        masked = PhonePattern.Replace(masked, match => $"{match.Groups["prefix"].Value}****{match.Groups["suffix"].Value}");
        masked = IdCardPattern.Replace(masked, match => $"{match.Groups["prefix"].Value}********{match.Groups["suffix"].Value}");
        masked = BankCardPattern.Replace(masked, match => $"{match.Groups["prefix"].Value}********{match.Groups["suffix"].Value}");

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

    private static string MaskPhone(string value)
    {
        return PhonePattern.Replace(value, match => $"{match.Groups["prefix"].Value}****{match.Groups["suffix"].Value}");
    }

    private static string MaskIdCard(string value)
    {
        return IdCardPattern.Replace(value, match => $"{match.Groups["prefix"].Value}********{match.Groups["suffix"].Value}");
    }

    private static string MaskBankCard(string value)
    {
        return BankCardPattern.Replace(value, match => $"{match.Groups["prefix"].Value}********{match.Groups["suffix"].Value}");
    }

    private static string MaskIpAddress(string value)
    {
        return Ipv4Pattern.Replace(value, match => $"{match.Groups["a"].Value}.{match.Groups["b"].Value}.{match.Groups["c"].Value}.*");
    }

    private static string MaskName(string value)
    {
        return value.Length <= 1 ? "*" : $"{value[0]}{new string('*', Math.Max(1, value.Length - 1))}";
    }

    private static string MaskAddress(string value)
    {
        if (value.Length <= 6)
            return "******";

        return $"{value[..3]}******{value[^3..]}";
    }
}
