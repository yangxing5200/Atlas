using Atlas.Core.Authorization;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Opportunities;
using Atlas.Modules.BidOps.Entities.Tendering;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsOpportunityMaintenanceService : IBidOpsOpportunityMaintenanceService
{
    private readonly IRepository<Opportunity> _opportunities;
    private readonly IRepository<OpportunityWatch> _watches;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<RequirementItem> _requirements;
    private readonly IUnitOfWork _unitOfWork;

    public BidOpsOpportunityMaintenanceService(
        IRepository<Opportunity> opportunities,
        IRepository<OpportunityWatch> watches,
        IRepository<TenderPackage> packages,
        IRepository<Notice> notices,
        IRepository<RequirementItem> requirements,
        IUnitOfWork unitOfWork)
    {
        _opportunities = opportunities ?? throw new ArgumentNullException(nameof(opportunities));
        _watches = watches ?? throw new ArgumentNullException(nameof(watches));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<BidOpsOpportunityMaintenanceResult> RunValueAssessmentAsync(
        int maxItems,
        CancellationToken ct = default)
    {
        maxItems = NormalizeMaxItems(maxItems);
        var now = DateTime.UtcNow;
        var query = await _opportunities.QueryDataScopeTrackingAsync(
            BidOpsDataResources.Opportunity,
            AtlasDataScopeType.AllTenant,
            ct);
        var opportunities = await query
            .Where(x =>
                x.Status == BidOpsOpportunityStatuses.Active &&
                (x.ValueScore == null || x.ValueLevel == BidOpsOpportunityValueLevels.Unknown))
            .OrderBy(x => x.CreatedAt)
            .Take(maxItems)
            .ToListAsync(ct);

        var packageMap = await LoadPackageMapAsync(opportunities.Select(x => x.PackageId).Distinct().ToArray(), ct);
        var riskMap = await LoadRejectRiskCountMapAsync(packageMap.Keys.ToArray(), ct);
        var updated = 0;

        foreach (var opportunity in opportunities)
        {
            packageMap.TryGetValue(opportunity.PackageId, out var package);
            var amount = opportunity.EstimatedAmount ?? package?.BudgetAmount ?? package?.MaxPrice;
            var rejectRiskCount = riskMap.GetValueOrDefault(opportunity.PackageId);
            var score = CalculateValueScore(amount, rejectRiskCount);
            var changed = false;

            if (opportunity.ValueScore == null)
            {
                opportunity.ValueScore = score;
                changed = true;
            }

            if (opportunity.ValueLevel == BidOpsOpportunityValueLevels.Unknown)
            {
                opportunity.ValueLevel = ValueLevelFromScore(opportunity.ValueScore ?? score);
                changed = true;
            }

            if (string.IsNullOrWhiteSpace(opportunity.AssessmentSummary))
            {
                opportunity.AssessmentSummary = "系统按预算金额、最高限价和废标风险生成初始价值评分，需人工复核。";
                changed = true;
            }

            if (changed)
            {
                opportunity.UpdatedAt = now;
                updated++;
            }
        }

        if (updated > 0)
            await _unitOfWork.SaveChangesAsync(ct);

        return new BidOpsOpportunityMaintenanceResult(
            opportunities.Count(),
            opportunities.Count(),
            updated,
            "initial value assessment completed");
    }

    public async Task<BidOpsOpportunityMaintenanceResult> RunDeadlineReminderScanAsync(
        int maxItems,
        int warningDays,
        CancellationToken ct = default)
    {
        var (opportunities, noticeMap) = await LoadActiveOpportunitiesWithNoticesAsync(maxItems, ct);
        var now = DateTime.UtcNow;
        var warningUntil = now.AddDays(NormalizeWarningDays(warningDays));
        var risks = opportunities
            .Where(x => noticeMap.TryGetValue(x.NoticeId, out var notice) &&
                        notice.BidDeadline.HasValue &&
                        notice.BidDeadline.Value <= warningUntil)
            .ToList();
        var overdue = risks.Count(x => noticeMap[x.NoticeId].BidDeadline!.Value < now);
        var urgent = risks.Count(x =>
            noticeMap[x.NoticeId].BidDeadline!.Value >= now &&
            noticeMap[x.NoticeId].BidDeadline!.Value <= now.AddDays(3));

        return new BidOpsOpportunityMaintenanceResult(
            opportunities.Count(),
            risks.Count(),
            0,
            $"deadline reminder scan completed;overdue={overdue};urgent={urgent}");
    }

    public async Task<BidOpsOpportunityMaintenanceResult> RunWatchReminderScanAsync(
        int maxItems,
        int warningDays,
        CancellationToken ct = default)
    {
        var watchQuery = await _watches.QueryDataScopeAsync(
            BidOpsDataResources.Opportunity,
            AtlasDataScopeType.AllTenant,
            ct);
        var watches = await watchQuery
            .Where(x => x.Enabled)
            .OrderBy(x => x.CreatedAt)
            .Take(NormalizeMaxItems(maxItems))
            .ToListAsync(ct);
        if (watches.Count() == 0)
        {
            return new BidOpsOpportunityMaintenanceResult(0, 0, 0, "watch reminder scan completed");
        }

        var opportunityIds = watches.Select(x => x.OpportunityId).Distinct().ToArray();
        var opportunityQuery = await _opportunities.QueryDataScopeAsync(
            BidOpsDataResources.Opportunity,
            AtlasDataScopeType.AllTenant,
            ct);
        var opportunities = await opportunityQuery
            .Where(x => opportunityIds.Contains(x.Id) && x.Status == BidOpsOpportunityStatuses.Active)
            .ToListAsync(ct);
        var opportunityMap = opportunities.ToDictionary(x => x.Id);
        var noticeMap = await LoadNoticeMapAsync(opportunities.Select(x => x.NoticeId).Distinct().ToArray(), ct);

        var now = DateTime.UtcNow;
        var warningUntil = now.AddDays(NormalizeWarningDays(warningDays));
        var matchedOpportunityIds = opportunities
            .Where(x => IsActionDue(x, now) || IsDeadlineDue(x, noticeMap, warningUntil))
            .Select(x => x.Id)
            .ToHashSet();
        var matchedWatches = watches.Count(x =>
            opportunityMap.ContainsKey(x.OpportunityId) &&
            matchedOpportunityIds.Contains(x.OpportunityId));

        return new BidOpsOpportunityMaintenanceResult(
            watches.Count(),
            matchedWatches,
            0,
            $"watch reminder scan completed;opportunities={matchedOpportunityIds.Count}");
    }

    public async Task<BidOpsOpportunityMaintenanceResult> RunStaleStateScanAsync(
        int maxItems,
        int staleDays,
        CancellationToken ct = default)
    {
        maxItems = NormalizeMaxItems(maxItems);
        staleDays = Math.Clamp(staleDays <= 0 ? 14 : staleDays, 1, 365);
        var now = DateTime.UtcNow;
        var staleBefore = now.AddDays(-staleDays);
        var query = await _opportunities.QueryDataScopeAsync(
            BidOpsDataResources.Opportunity,
            AtlasDataScopeType.AllTenant,
            ct);
        var opportunities = await query
            .Where(x => x.Status == BidOpsOpportunityStatuses.Active)
            .OrderBy(x => x.LastStageChangedAtUtc)
            .Take(maxItems)
            .ToListAsync(ct);
        var stale = opportunities
            .Where(x => x.LastStageChangedAtUtc <= staleBefore &&
                        (!x.NextActionAtUtc.HasValue || x.NextActionAtUtc.Value <= now))
            .ToList();

        return new BidOpsOpportunityMaintenanceResult(
            opportunities.Count(),
            stale.Count(),
            0,
            $"stale opportunity scan completed;staleDays={staleDays}");
    }

    private async Task<(List<Opportunity> Opportunities, Dictionary<long, Notice> NoticeMap)> LoadActiveOpportunitiesWithNoticesAsync(
        int maxItems,
        CancellationToken ct)
    {
        var query = await _opportunities.QueryDataScopeAsync(
            BidOpsDataResources.Opportunity,
            AtlasDataScopeType.AllTenant,
            ct);
        var opportunities = await query
            .Where(x => x.Status == BidOpsOpportunityStatuses.Active)
            .OrderBy(x => x.NextActionAtUtc)
            .Take(NormalizeMaxItems(maxItems))
            .ToListAsync(ct);
        var noticeMap = await LoadNoticeMapAsync(opportunities.Select(x => x.NoticeId).Distinct().ToArray(), ct);
        return (opportunities, noticeMap);
    }

    private async Task<Dictionary<long, TenderPackage>> LoadPackageMapAsync(
        IReadOnlyCollection<long> packageIds,
        CancellationToken ct)
    {
        if (packageIds.Count == 0)
            return new Dictionary<long, TenderPackage>();

        var query = await _packages.QueryDataScopeAsync(
            BidOpsDataResources.TenderPackage,
            AtlasDataScopeType.AllTenant,
            ct);
        var packages = await query
            .Where(x => packageIds.Contains(x.Id))
            .ToListAsync(ct);
        return packages.ToDictionary(x => x.Id);
    }

    private async Task<Dictionary<long, Notice>> LoadNoticeMapAsync(
        IReadOnlyCollection<long> noticeIds,
        CancellationToken ct)
    {
        if (noticeIds.Count == 0)
            return new Dictionary<long, Notice>();

        var query = await _notices.QueryDataScopeAsync(
            BidOpsDataResources.Notice,
            AtlasDataScopeType.AllTenant,
            ct);
        var notices = await query
            .Where(x => noticeIds.Contains(x.Id))
            .ToListAsync(ct);
        return notices.ToDictionary(x => x.Id);
    }

    private async Task<Dictionary<long, int>> LoadRejectRiskCountMapAsync(
        IReadOnlyCollection<long> packageIds,
        CancellationToken ct)
    {
        if (packageIds.Count == 0)
            return new Dictionary<long, int>();

        var query = await _requirements.QueryDataScopeAsync(
            BidOpsDataResources.TenderPackage,
            AtlasDataScopeType.AllTenant,
            ct);
        var requirements = await query
            .Where(x => packageIds.Contains(x.PackageId) && x.IsRejectRisk)
            .ToListAsync(ct);
        return requirements
            .GroupBy(x => x.PackageId)
            .ToDictionary(x => x.Key, x => x.Count());
    }

    private static decimal CalculateValueScore(decimal? amount, int rejectRiskCount)
    {
        var score = 45m;
        if (amount >= 1_000_000m)
            score += 30m;
        else if (amount >= 300_000m)
            score += 20m;
        else if (amount >= 100_000m)
            score += 10m;

        score -= Math.Min(20, rejectRiskCount * 8);
        return Math.Clamp(score, 0m, 100m);
    }

    private static string ValueLevelFromScore(decimal score)
    {
        if (score >= 75m)
            return BidOpsOpportunityValueLevels.High;
        if (score >= 50m)
            return BidOpsOpportunityValueLevels.Medium;
        return BidOpsOpportunityValueLevels.Low;
    }

    private static bool IsActionDue(Opportunity opportunity, DateTime now)
    {
        return opportunity.NextActionAtUtc.HasValue && opportunity.NextActionAtUtc.Value <= now;
    }

    private static bool IsDeadlineDue(
        Opportunity opportunity,
        IReadOnlyDictionary<long, Notice> noticeMap,
        DateTime warningUntil)
    {
        return noticeMap.TryGetValue(opportunity.NoticeId, out var notice) &&
               notice.BidDeadline.HasValue &&
               notice.BidDeadline.Value <= warningUntil;
    }

    private static int NormalizeMaxItems(int maxItems)
    {
        return Math.Clamp(maxItems <= 0 ? 100 : maxItems, 1, 1000);
    }

    private static int NormalizeWarningDays(int warningDays)
    {
        return Math.Clamp(warningDays <= 0 ? 7 : warningDays, 1, 60);
    }
}
