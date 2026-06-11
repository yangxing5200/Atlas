using Microsoft.Extensions.Configuration;

namespace Atlas.Modules.BidOps.BackgroundJobs;

internal static class BidOpsBackgroundTenantConfiguration
{
    public static IReadOnlyList<long> GetTenantIds(
        IConfiguration configuration,
        string sectionName)
    {
        var section = configuration.GetSection($"BidOps:{sectionName}:TenantIds");
        var ids = section.GetChildren()
            .Select(x => long.TryParse(x.Value, out var id) ? id : 0)
            .Where(x => x > 0)
            .Distinct()
            .ToArray();

        if (ids.Length > 0)
            return ids;

        var single = configuration[$"BidOps:{sectionName}:TenantId"];
        return long.TryParse(single, out var tenantId) && tenantId > 0
            ? [tenantId]
            : Array.Empty<long>();
    }

    public static long GetUserId(
        IConfiguration configuration,
        string sectionName)
    {
        return Math.Max(0, configuration.GetValue<long?>($"BidOps:{sectionName}:UserId") ?? 0);
    }

    public static string GetUserName(
        IConfiguration configuration,
        string sectionName,
        string fallback)
    {
        var configured = configuration[$"BidOps:{sectionName}:UserName"];
        return string.IsNullOrWhiteSpace(configured) ? fallback : configured.Trim();
    }
}
