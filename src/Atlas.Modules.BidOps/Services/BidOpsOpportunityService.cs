using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Entities.Opportunities;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsOpportunityService : IBidOpsOpportunityService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly IRepository<Opportunity> _opportunities;
    private readonly IRepository<OpportunityStageHistory> _stageHistories;
    private readonly IRepository<OpportunityWatch> _watches;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<RequirementItem> _requirements;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentIdentity _current;
    private readonly IIdGenerator _idGenerator;

    public BidOpsOpportunityService(
        IRepository<Opportunity> opportunities,
        IRepository<OpportunityStageHistory> stageHistories,
        IRepository<OpportunityWatch> watches,
        IRepository<TenderPackage> packages,
        IRepository<Notice> notices,
        IRepository<RequirementItem> requirements,
        IUnitOfWork unitOfWork,
        ICurrentIdentity current,
        IIdGenerator idGenerator)
    {
        _opportunities = opportunities ?? throw new ArgumentNullException(nameof(opportunities));
        _stageHistories = stageHistories ?? throw new ArgumentNullException(nameof(stageHistories));
        _watches = watches ?? throw new ArgumentNullException(nameof(watches));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<PagedResult<OpportunityDto>> SearchAsync(
        OpportunitySearchQuery query,
        CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _opportunities.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.Title.Contains(keyword) ||
                x.OpportunityNo.Contains(keyword) ||
                x.AssessmentSummary.Contains(keyword) ||
                x.Remark.Contains(keyword));
        }

        if (query.NoticeId.HasValue)
            builder = builder.Where(x => x.NoticeId == query.NoticeId.Value);

        if (query.PackageId.HasValue)
            builder = builder.Where(x => x.PackageId == query.PackageId.Value);

        if (!string.IsNullOrWhiteSpace(query.Stage))
        {
            var stage = query.Stage.Trim();
            builder = builder.Where(x => x.Stage == stage);
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            builder = builder.Where(x => x.Status == status);
        }

        if (query.WatchedByMe == true && _current.UserId.HasValue)
        {
            var userId = _current.UserId.Value;
            var watchQuery = await _watches.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);
            var watchedItems = await watchQuery
                .Where(x => x.UserId == userId && x.Enabled)
                .ToListAsync(ct);
            var watchedIds = watchedItems.Select(x => x.OpportunityId).Distinct().ToArray();
            if (watchedIds.Length == 0)
            {
                return new PagedResult<OpportunityDto>(
                    0,
                    [],
                    pageIndex,
                    pageSize);
            }

            builder = builder.Where(x => watchedIds.Contains(x.Id));
        }

        var total = await builder.CountAsync(ct);
        var items = await builder
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OpportunityDto>(
            total,
            await MapListAsync(items, ct),
            pageIndex,
            pageSize);
    }

    public async Task<OpportunityDetailDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var opportunity = await GetOpportunityAsync(id, tracking: false, ct);
        if (opportunity == null)
            return null;

        var package = await LoadPackageDtoAsync(opportunity.PackageId, ct);
        var requirementQuery = await _requirements.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        var requirements = await requirementQuery
            .Where(x => x.PackageId == opportunity.PackageId)
            .OrderBy(x => x.Id)
            .Select(x => new RequirementItemDto
            {
                Id = x.Id,
                PackageId = x.PackageId,
                RequirementType = x.RequirementType,
                OriginalText = x.OriginalText,
                IsMandatory = x.IsMandatory,
                IsRejectRisk = x.IsRejectRisk,
                RequiredEvidenceType = x.RequiredEvidenceType,
                RiskLevel = x.RiskLevel
            })
            .ToListAsync(ct);
        var historyQuery = await _stageHistories.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);
        var history = await historyQuery
            .Where(x => x.OpportunityId == id)
            .OrderByDescending(x => x.OccurredAtUtc)
            .Select(x => new OpportunityStageHistoryDto
            {
                Id = x.Id,
                OpportunityId = x.OpportunityId,
                FromStage = x.FromStage,
                ToStage = x.ToStage,
                Reason = x.Reason,
                OperatorUserId = x.OperatorUserId,
                OccurredAtUtc = x.OccurredAtUtc
            })
            .ToListAsync(ct);

        return new OpportunityDetailDto
        {
            Opportunity = (await MapListAsync(new[] { opportunity }, ct)).Single(),
            Package = package,
            Requirements = requirements,
            StageHistory = history
        };
    }

    public async Task<OpportunityDto> CreateAsync(
        CreateOpportunityRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var package = await GetPackageAsync(request.PackageId, ct)
            ?? throw new AtlasException($"BidOps package does not exist: {request.PackageId}");
        await EnsureNoActiveOpportunityAsync(package.Id, excludingOpportunityId: null, ct);

        var now = DateTime.UtcNow;
        var id = _idGenerator.NextId();
        var title = string.IsNullOrWhiteSpace(request.Title)
            ? BuildOpportunityTitle(package)
            : request.Title.Trim();
        var opportunity = new Opportunity
        {
            Id = id,
            TenantId = tenantId,
            NoticeId = package.NoticeId,
            PackageId = package.Id,
            OpportunityNo = $"OPP-{now:yyyyMMdd}-{Math.Abs(id % 1_000_000):D6}",
            Title = Truncate(title, 500),
            Stage = BidOpsOpportunityStages.New,
            Status = BidOpsOpportunityStatuses.Active,
            ActiveMarker = BidOpsOpportunityActiveMarkers.Active,
            Priority = NormalizePriority(request.Priority),
            EstimatedAmount = request.EstimatedAmount ?? package.BudgetAmount ?? package.MaxPrice,
            OwnerUserId = request.OwnerUserId ?? _current.UserId,
            NextActionAtUtc = request.NextActionAtUtc,
            LastStageChangedAtUtc = now,
            Remark = Truncate(request.Remark, 1000),
            CreatedAt = now
        };
        var history = CreateStageHistory(opportunity, string.Empty, opportunity.Stage, "创建商机", now);

        await _opportunities.AddAsync(opportunity, ct);
        await _stageHistories.AddAsync(history, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        return (await MapListAsync(new[] { opportunity }, ct)).Single();
    }

    public async Task<OpportunityDto> UpdateAsync(
        long id,
        UpdateOpportunityRequest request,
        CancellationToken ct = default)
    {
        var opportunity = await GetOpportunityAsync(id, tracking: true, ct)
            ?? throw new AtlasException($"BidOps opportunity does not exist: {id}");

        if (!string.IsNullOrWhiteSpace(request.Title))
            opportunity.Title = Truncate(request.Title.Trim(), 500);
        if (request.Priority.HasValue)
            opportunity.Priority = NormalizePriority(request.Priority.Value);
        if (request.EstimatedAmount.HasValue)
            opportunity.EstimatedAmount = request.EstimatedAmount.Value;
        if (request.OwnerUserId.HasValue)
            opportunity.OwnerUserId = request.OwnerUserId.Value;
        if (request.NextActionAtUtc.HasValue)
            opportunity.NextActionAtUtc = request.NextActionAtUtc.Value;
        if (request.Remark != null)
            opportunity.Remark = Truncate(request.Remark, 1000);

        opportunity.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return (await MapListAsync(new[] { opportunity }, ct)).Single();
    }

    public async Task<OpportunityDto> WatchAsync(
        long id,
        WatchOpportunityRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var userId = _current.UserId ?? throw new AtlasException("BidOps opportunity watch requires an authenticated user.");
        var opportunity = await GetOpportunityAsync(id, tracking: false, ct)
            ?? throw new AtlasException($"BidOps opportunity does not exist: {id}");
        var now = DateTime.UtcNow;
        var watchQuery = await _watches.QueryTrackingAsync(ct);
        var watch = await watchQuery
            .Where(x => x.TenantId == tenantId && x.OpportunityId == id && x.UserId == userId)
            .FirstOrDefaultAsync(ct);
        if (watch == null)
        {
            watch = new OpportunityWatch
            {
                Id = _idGenerator.NextId(),
                TenantId = tenantId,
                OpportunityId = id,
                UserId = userId,
                Enabled = request.Enabled,
                Remark = Truncate(request.Remark, 500),
                CreatedAt = now
            };
            await _watches.AddAsync(watch, ct);
        }
        else
        {
            watch.Enabled = request.Enabled;
            watch.Remark = Truncate(request.Remark, 500);
            watch.UpdatedAt = now;
        }

        await _unitOfWork.SaveChangesAsync(ct);
        return (await MapListAsync(new[] { opportunity }, ct)).Single();
    }

    public async Task<OpportunityDto> AssessAsync(
        long id,
        AssessOpportunityRequest request,
        CancellationToken ct = default)
    {
        var opportunity = await GetOpportunityAsync(id, tracking: true, ct)
            ?? throw new AtlasException($"BidOps opportunity does not exist: {id}");
        var now = DateTime.UtcNow;

        if (request.ValueScore.HasValue)
            opportunity.ValueScore = Math.Clamp(request.ValueScore.Value, 0, 100);
        if (!string.IsNullOrWhiteSpace(request.ValueLevel))
            opportunity.ValueLevel = NormalizeValueLevel(request.ValueLevel);
        if (!string.IsNullOrWhiteSpace(request.Decision))
            opportunity.Decision = NormalizeDecision(request.Decision);
        if (request.AssessmentSummary != null)
            opportunity.AssessmentSummary = Truncate(request.AssessmentSummary, 2000);

        var targetStage = opportunity.Decision is BidOpsOpportunityDecisions.Go or BidOpsOpportunityDecisions.NoGo
            ? BidOpsOpportunityStages.Decided
            : BidOpsOpportunityStages.Assessing;
        await ChangeStageCoreAsync(opportunity, targetStage, "商机评估", now, ct);
        opportunity.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(ct);
        return (await MapListAsync(new[] { opportunity }, ct)).Single();
    }

    public async Task<OpportunityDto> ChangeStageAsync(
        long id,
        ChangeOpportunityStageRequest request,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.Stage))
            throw new AtlasException("BidOps opportunity stage is required.");

        var opportunity = await GetOpportunityAsync(id, tracking: true, ct)
            ?? throw new AtlasException($"BidOps opportunity does not exist: {id}");
        var now = DateTime.UtcNow;
        var targetStatus = string.IsNullOrWhiteSpace(request.Status)
            ? opportunity.Status
            : NormalizeStatus(request.Status);

        if (targetStatus == BidOpsOpportunityStatuses.Active)
        {
            await EnsureNoActiveOpportunityAsync(opportunity.PackageId, opportunity.Id, ct);
            opportunity.ActiveMarker = BidOpsOpportunityActiveMarkers.Active;
        }
        else
        {
            opportunity.ActiveMarker = null;
        }

        opportunity.Status = targetStatus;
        await ChangeStageCoreAsync(opportunity, request.Stage.Trim(), request.Reason ?? "阶段流转", now, ct);
        opportunity.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(ct);
        return (await MapListAsync(new[] { opportunity }, ct)).Single();
    }

    private async Task ChangeStageCoreAsync(
        Opportunity opportunity,
        string targetStage,
        string reason,
        DateTime now,
        CancellationToken ct)
    {
        targetStage = Truncate(targetStage, 64);
        if (opportunity.Stage == targetStage)
            return;

        var fromStage = opportunity.Stage;
        opportunity.Stage = targetStage;
        opportunity.LastStageChangedAtUtc = now;
        await _stageHistories.AddAsync(CreateStageHistory(opportunity, fromStage, targetStage, reason, now), ct);
    }

    private async Task EnsureNoActiveOpportunityAsync(
        long packageId,
        long? excludingOpportunityId,
        CancellationToken ct)
    {
        var builder = await _opportunities.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);
        builder = builder.Where(x =>
            x.PackageId == packageId &&
            x.ActiveMarker == BidOpsOpportunityActiveMarkers.Active);
        if (excludingOpportunityId.HasValue)
            builder = builder.Where(x => x.Id != excludingOpportunityId.Value);

        var exists = await builder.AnyAsync(ct);
        if (exists)
            throw new AtlasException("该包件已存在有效商机，MVP 阶段一个包件最多只能有一个有效商机。");
    }

    private async Task<Opportunity?> GetOpportunityAsync(long id, bool tracking, CancellationToken ct)
    {
        var builder = tracking
            ? await _opportunities.QueryDataScopeTrackingAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct)
            : await _opportunities.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);
        return await builder.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private async Task<TenderPackage?> GetPackageAsync(long id, CancellationToken ct)
    {
        var builder = await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await builder.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private async Task<List<OpportunityDto>> MapListAsync(
        IReadOnlyCollection<Opportunity> opportunities,
        CancellationToken ct)
    {
        if (opportunities.Count == 0)
            return [];

        var packageIds = opportunities.Select(x => x.PackageId).Distinct().ToArray();
        var noticeIds = opportunities.Select(x => x.NoticeId).Distinct().ToArray();
        var opportunityIds = opportunities.Select(x => x.Id).ToArray();
        var packageQuery = await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        var packageItems = await packageQuery
            .Where(x => packageIds.Contains(x.Id))
            .ToListAsync(ct);
        var packages = packageItems.ToDictionary(x => x.Id);
        var noticeQuery = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        var noticeItems = await noticeQuery
            .Where(x => noticeIds.Contains(x.Id))
            .ToListAsync(ct);
        var notices = noticeItems.ToDictionary(x => x.Id);
        var watchQuery = await _watches.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);
        var watchItems = await watchQuery
            .Where(x => opportunityIds.Contains(x.OpportunityId) && x.Enabled)
            .ToListAsync(ct);
        var watchCounts = watchItems
            .GroupBy(x => x.OpportunityId)
            .ToDictionary(x => x.Key, x => x.Count());
        var watchedByMe = _current.UserId.HasValue
            ? watchItems
                .Where(x => x.UserId == _current.UserId.Value)
                .Select(x => x.OpportunityId)
                .ToList()
            : new List<long>();
        var watchedSet = watchedByMe.ToHashSet();

        return opportunities.Select(x =>
        {
            packages.TryGetValue(x.PackageId, out var package);
            notices.TryGetValue(x.NoticeId, out var notice);
            return new OpportunityDto
            {
                Id = x.Id,
                NoticeId = x.NoticeId,
                PackageId = x.PackageId,
                OpportunityNo = x.OpportunityNo,
                Title = x.Title,
                NoticeTitle = notice?.Title ?? string.Empty,
                PackageName = package?.PackageName ?? string.Empty,
                PackageNo = package?.PackageNo ?? string.Empty,
                ProjectName = notice?.ProjectName ?? string.Empty,
                ProjectCode = notice?.ProjectCode ?? string.Empty,
                BuyerName = notice?.BuyerName ?? string.Empty,
                Region = notice?.Region ?? string.Empty,
                PublishTime = notice?.PublishTime,
                BidDeadline = notice?.BidDeadline,
                Stage = x.Stage,
                Status = x.Status,
                Priority = x.Priority,
                EstimatedAmount = x.EstimatedAmount,
                ValueScore = x.ValueScore,
                ValueLevel = x.ValueLevel,
                Decision = x.Decision,
                OwnerUserId = x.OwnerUserId,
                NextActionAtUtc = x.NextActionAtUtc,
                LastStageChangedAtUtc = x.LastStageChangedAtUtc,
                AssessmentSummary = x.AssessmentSummary,
                Remark = x.Remark,
                WatchCount = watchCounts.TryGetValue(x.Id, out var count) ? count : 0,
                WatchedByMe = watchedSet.Contains(x.Id),
                CreatedAt = x.CreatedAt,
                UpdatedAt = x.UpdatedAt
            };
        }).ToList();
    }

    private async Task<TenderPackageDto?> LoadPackageDtoAsync(long packageId, CancellationToken ct)
    {
        var package = await GetPackageAsync(packageId, ct);
        if (package == null)
            return null;

        var noticeQuery = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        var notice = await noticeQuery.Where(x => x.Id == package.NoticeId).FirstOrDefaultAsync(ct);
        var requirementQuery = await _requirements.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        var packageRequirements = await requirementQuery
            .Where(x => x.PackageId == package.Id)
            .ToListAsync(ct);

        return new TenderPackageDto
        {
            Id = package.Id,
            NoticeId = package.NoticeId,
            NoticeTitle = notice?.Title ?? string.Empty,
            NoticeType = notice?.NoticeType ?? string.Empty,
            ProjectName = notice?.ProjectName ?? string.Empty,
            ProjectCode = notice?.ProjectCode ?? string.Empty,
            BuyerName = notice?.BuyerName ?? string.Empty,
            Region = notice?.Region ?? string.Empty,
            PublishTime = notice?.PublishTime,
            BidDeadline = notice?.BidDeadline,
            LotNo = package.LotNo,
            LotName = package.LotName,
            PackageNo = package.PackageNo,
            PackageName = package.PackageName,
            Category = package.Category,
            Quantity = package.Quantity,
            Unit = package.Unit,
            BudgetAmount = package.BudgetAmount,
            MaxPrice = package.MaxPrice,
            DeliveryPlace = package.DeliveryPlace,
            DeliveryPeriod = package.DeliveryPeriod,
            Status = package.Status,
            RequirementCount = packageRequirements.Count,
            RejectRiskCount = packageRequirements.Count(x => x.IsRejectRisk),
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        };
    }

    private OpportunityStageHistory CreateStageHistory(
        Opportunity opportunity,
        string fromStage,
        string toStage,
        string reason,
        DateTime now)
    {
        return new OpportunityStageHistory
        {
            Id = _idGenerator.NextId(),
            TenantId = opportunity.TenantId,
            OpportunityId = opportunity.Id,
            FromStage = Truncate(fromStage, 64),
            ToStage = Truncate(toStage, 64),
            Reason = Truncate(reason, 1000),
            OperatorUserId = _current.UserId,
            OccurredAtUtc = now,
            CreatedAt = now
        };
    }

    private long RequireTenantId()
    {
        return _current.TenantId ?? throw new AtlasException("BidOps tenant context is required.");
    }

    private static (int PageIndex, int PageSize) NormalizePaging(BidOpsPagedQuery query)
    {
        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        return (pageIndex, pageSize);
    }

    private static int NormalizePriority(int value) => Math.Clamp(value, 1, 5);

    private static string NormalizeValueLevel(string? value)
    {
        return value?.Trim() switch
        {
            BidOpsOpportunityValueLevels.Low => BidOpsOpportunityValueLevels.Low,
            BidOpsOpportunityValueLevels.Medium => BidOpsOpportunityValueLevels.Medium,
            BidOpsOpportunityValueLevels.High => BidOpsOpportunityValueLevels.High,
            _ => BidOpsOpportunityValueLevels.Unknown
        };
    }

    private static string NormalizeDecision(string? value)
    {
        return value?.Trim() switch
        {
            BidOpsOpportunityDecisions.Go => BidOpsOpportunityDecisions.Go,
            BidOpsOpportunityDecisions.NoGo => BidOpsOpportunityDecisions.NoGo,
            BidOpsOpportunityDecisions.Hold => BidOpsOpportunityDecisions.Hold,
            _ => BidOpsOpportunityDecisions.Undecided
        };
    }

    private static string NormalizeStatus(string? value)
    {
        return value?.Trim() switch
        {
            BidOpsOpportunityStatuses.Closed => BidOpsOpportunityStatuses.Closed,
            BidOpsOpportunityStatuses.Archived => BidOpsOpportunityStatuses.Archived,
            _ => BidOpsOpportunityStatuses.Active
        };
    }

    private static string BuildOpportunityTitle(TenderPackage package)
    {
        if (!string.IsNullOrWhiteSpace(package.PackageName))
            return package.PackageName;
        if (!string.IsNullOrWhiteSpace(package.LotName))
            return package.LotName;
        if (!string.IsNullOrWhiteSpace(package.PackageNo))
            return $"包件 {package.PackageNo}";

        return $"包件 {package.Id}";
    }

    private static string Truncate(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        var text = value.Trim();
        return text.Length <= maxLength ? text : text[..maxLength];
    }
}
