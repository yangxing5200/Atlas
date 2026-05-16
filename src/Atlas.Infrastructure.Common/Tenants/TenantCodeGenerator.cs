using System.Text;

namespace Atlas.Infrastructure.Common.Tenants;

public interface ITenantCodeGenerator
{
    string NormalizeDomain(string domainOrName);
    string GenerateStoreCode(string tenantDomain, string suffix);
}

public sealed class TenantCodeGenerator : ITenantCodeGenerator
{
    public string NormalizeDomain(string domainOrName)
    {
        if (string.IsNullOrWhiteSpace(domainOrName))
            throw new ArgumentException("Tenant domain is required.", nameof(domainOrName));

        var builder = new StringBuilder(domainOrName.Length);
        var previousDash = false;

        foreach (var ch in domainOrName.Trim().ToLowerInvariant())
        {
            if (char.IsLetterOrDigit(ch))
            {
                builder.Append(ch);
                previousDash = false;
                continue;
            }

            if (!previousDash)
            {
                builder.Append('-');
                previousDash = true;
            }
        }

        var value = builder.ToString().Trim('-');
        if (value.Length == 0)
            throw new ArgumentException("Tenant domain must contain letters or digits.", nameof(domainOrName));

        return value;
    }

    public string GenerateStoreCode(string tenantDomain, string suffix)
    {
        var normalizedDomain = NormalizeDomain(tenantDomain).Replace("-", string.Empty);
        var normalizedSuffix = NormalizeDomain(suffix).Replace("-", string.Empty);
        var domainPart = normalizedDomain.Length <= 12 ? normalizedDomain : normalizedDomain[..12];
        var suffixPart = normalizedSuffix.Length <= 8 ? normalizedSuffix : normalizedSuffix[..8];

        return $"{domainPart}-{suffixPart}".ToUpperInvariant();
    }
}
