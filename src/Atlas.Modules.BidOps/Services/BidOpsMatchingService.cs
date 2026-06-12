using Atlas.BackgroundTasks;
using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps;
using Atlas.Modules.BidOps.Entities.Matching;
using Atlas.Modules.BidOps.Entities.Opportunities;
using Atlas.Modules.BidOps.Entities.Suppliers;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsMatchingService : IBidOpsMatchingService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly IRepository<SupplierMatchRun> _runs;
    private readonly IRepository<SupplierMatchResult> _results;
    private readonly IRepository<MissingEvidenceCheck> _missingEvidenceChecks;
    private readonly IRepository<GoNoGoDecision> _decisions;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<RequirementItem> _requirements;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<Supplier> _suppliers;
    private readonly IRepository<SupplierCapability> _capabilities;
    private readonly IRepository<SupplierEvidenceDocument> _evidenceDocuments;
    private readonly IRepository<Opportunity> _opportunities;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IBackgroundJobClient _jobs;
    private readonly ICurrentIdentity _current;
    private readonly IIdGenerator _idGenerator;

    public BidOpsMatchingService(
        IRepository<SupplierMatchRun> runs,
        IRepository<SupplierMatchResult> results,
        IRepository<MissingEvidenceCheck> missingEvidenceChecks,
        IRepository<GoNoGoDecision> decisions,
        IRepository<TenderPackage> packages,
        IRepository<RequirementItem> requirements,
        IRepository<Notice> notices,
        IRepository<Supplier> suppliers,
        IRepository<SupplierCapability> capabilities,
        IRepository<SupplierEvidenceDocument> evidenceDocuments,
        IRepository<Opportunity> opportunities,
        IUnitOfWork unitOfWork,
        IBackgroundJobClient jobs,
        ICurrentIdentity current,
        IIdGenerator idGenerator)
    {
        _runs = runs ?? throw new ArgumentNullException(nameof(runs));
        _results = results ?? throw new ArgumentNullException(nameof(results));
        _missingEvidenceChecks = missingEvidenceChecks ?? throw new ArgumentNullException(nameof(missingEvidenceChecks));
        _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _requirements = requirements ?? throw new ArgumentNullException(nameof(requirements));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _evidenceDocuments = evidenceDocuments ?? throw new ArgumentNullException(nameof(evidenceDocuments));
        _opportunities = opportunities ?? throw new ArgumentNullException(nameof(opportunities));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<StartSupplierMatchRunResponse> StartSupplierMatchRunAsync(
        long packageId,
        StartSupplierMatchRunRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        var package = await GetPackageAsync(packageId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps package does not exist: {packageId}");

        var now = DateTime.UtcNow;
        var runId = _idGenerator.NextId();
        var maxSuppliers = Math.Clamp(request.MaxSuppliers <= 0 ? 100 : request.MaxSuppliers, 1, 500);
        var run = new SupplierMatchRun
        {
            Id = runId,
            TenantId = tenantId,
            PackageId = packageId,
            RunNo = $"MATCH-{now:yyyyMMdd}-{Math.Abs(runId % 1_000_000):D6}",
            Status = BidOpsSupplierMatchRunStatuses.Queued,
            RequestedByUserId = userId,
            RequestedByUserName = Truncate(_current.UserName, 128),
            CriteriaSummary = BuildCriteriaSummary(package, request.CriteriaSummary),
            MaxSuppliers = maxSuppliers,
            CreatedAt = now
        };

        await _runs.AddAsync(run, ct);
        await _unitOfWork.SaveChangesAsync(ct);

        try
        {
            var result = await _jobs.EnqueueAsync(
                new EnqueueBackgroundJobRequest<SupplierMatchRunJobPayload>
                {
                    JobType = BidOpsBackgroundJobTypes.SupplierMatchRun,
                    Queue = BidOpsBackgroundJobQueues.BidOps,
                    JobName = "BidOps supplier match run",
                    TenantId = tenantId,
                    StoreId = _current.StoreId,
                    DeduplicationKey = $"bidops:matching:supplier-match-run:{tenantId}:{runId}",
                    MaxAttempts = 3,
                    Payload = new SupplierMatchRunJobPayload(
                        tenantId,
                        _current.StoreId,
                        userId,
                        _current.UserName,
                        runId,
                        maxSuppliers)
                },
                ct);

            run.BackgroundJobId = result.JobId;
            run.UpdatedAt = DateTime.UtcNow;
            await _unitOfWork.SaveChangesAsync(ct);

            return new StartSupplierMatchRunResponse
            {
                Run = MapRun(run),
                Job = new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists)
            };
        }
        catch (Exception ex)
        {
            run.Status = BidOpsSupplierMatchRunStatuses.Failed;
            run.ErrorMessage = Truncate(ex.Message, 2000);
            run.CompletedAtUtc = DateTime.UtcNow;
            run.UpdatedAt = run.CompletedAtUtc;
            await _unitOfWork.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<PagedResult<SupplierMatchRunDto>> SearchRunsAsync(
        SupplierMatchRunSearchQuery query,
        CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _runs.QueryDataScopeAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x => x.RunNo.Contains(keyword) || x.CriteriaSummary.Contains(keyword));
        }

        if (query.PackageId.HasValue)
            builder = builder.Where(x => x.PackageId == query.PackageId.Value);

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            builder = builder.Where(x => x.Status == status);
        }

        var total = await builder.CountAsync(ct);
        var runs = await builder
            .OrderByDescending(x => x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SupplierMatchRunDto>(total, runs.Select(MapRun).ToList(), pageIndex, pageSize);
    }

    public async Task<SupplierMatchRunDetailDto?> GetRunAsync(long runId, CancellationToken ct = default)
    {
        var run = await GetRunAsync(runId, tracking: false, ct);
        if (run == null)
            return null;

        var package = await GetPackageAsync(run.PackageId, tracking: false, ct);
        var notice = package == null ? null : await GetNoticeAsync(package.NoticeId, ct);
        var requirements = package == null ? [] : await ListRequirementsAsync(package.Id, ct);

        return new SupplierMatchRunDetailDto
        {
            Run = MapRun(run),
            Package = package == null ? null : MapPackage(package, notice, requirements),
            Requirements = requirements.Select(MapRequirement).ToList(),
            Results = await ListRunResultsCoreAsync(runId, ct)
        };
    }

    public async Task<IReadOnlyList<SupplierMatchResultDto>> ListRunResultsAsync(
        long runId,
        CancellationToken ct = default)
    {
        var run = await GetRunAsync(runId, tracking: false, ct);
        return run == null ? [] : await ListRunResultsCoreAsync(runId, ct);
    }

    public async Task<BidOpsSupplierMatchExecutionResult> ExecuteSupplierMatchRunAsync(
        long runId,
        int maxSuppliers,
        CancellationToken ct = default)
    {
        var run = await GetRunAsync(runId, tracking: true, ct)
            ?? throw new AtlasException($"BidOps supplier match run does not exist: {runId}");
        var now = DateTime.UtcNow;
        run.Status = BidOpsSupplierMatchRunStatuses.Running;
        run.StartedAtUtc ??= now;
        run.ErrorMessage = string.Empty;
        run.UpdatedAt = now;
        await _unitOfWork.SaveChangesAsync(ct);

        try
        {
            var package = await GetPackageAsync(run.PackageId, tracking: false, ct)
                ?? throw new AtlasException($"BidOps package does not exist: {run.PackageId}");
            var notice = await GetNoticeAsync(package.NoticeId, ct);
            var requirements = await ListRequirementsAsync(package.Id, ct);
            var suppliers = await ListActiveSuppliersAsync(Math.Clamp(maxSuppliers <= 0 ? run.MaxSuppliers : maxSuppliers, 1, 500), ct);
            var supplierIds = suppliers.Select(x => x.Id).ToArray();
            var capabilities = await ListCapabilitiesAsync(supplierIds, ct);
            var evidenceDocuments = await ListEvidenceDocumentsAsync(supplierIds, ct);

            var oldChecksQuery = await _missingEvidenceChecks.QueryDataScopeTrackingAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct);
            var oldChecks = await oldChecksQuery.Where(x => x.RunId == run.Id).ToListAsync(ct);
            var oldResultsQuery = await _results.QueryDataScopeTrackingAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct);
            var oldResults = await oldResultsQuery.Where(x => x.RunId == run.Id).ToListAsync(ct);
            if (oldChecks.Count > 0)
                await _missingEvidenceChecks.RemoveRangeAsync(oldChecks, ct);
            if (oldResults.Count > 0)
                await _results.RemoveRangeAsync(oldResults, ct);

            var resultDrafts = suppliers
                .Select(supplier => BuildMatchResult(
                    run,
                    package,
                    notice,
                    requirements,
                    supplier,
                    capabilities.Where(x => x.SupplierId == supplier.Id).ToList(),
                    evidenceDocuments.Where(x => x.SupplierId == supplier.Id).ToList(),
                    now))
                .OrderByDescending(x => x.Result.Score)
                .ThenBy(x => x.Result.MissingEvidenceCount)
                .ThenBy(x => x.Result.SupplierNameSnapshot)
                .ToList();

            var rank = 1;
            foreach (var draft in resultDrafts)
                draft.Result.Rank = rank++;

            var results = resultDrafts.Select(x => x.Result).ToList();
            var missingEvidence = resultDrafts.SelectMany(x => x.MissingEvidenceChecks).ToList();
            if (results.Count > 0)
                await _results.AddRangeAsync(results, ct);
            if (missingEvidence.Count > 0)
                await _missingEvidenceChecks.AddRangeAsync(missingEvidence, ct);

            run.Status = BidOpsSupplierMatchRunStatuses.Succeeded;
            run.SupplierCount = suppliers.Count;
            run.MatchedCount = results.Count(x =>
                x.Recommendation is BidOpsSupplierMatchRecommendations.Candidate or BidOpsSupplierMatchRecommendations.Caution);
            run.MissingEvidenceCount = missingEvidence.Count(x =>
                x.Status is BidOpsMissingEvidenceStatuses.Missing or BidOpsMissingEvidenceStatuses.Expired);
            run.CompletedAtUtc = DateTime.UtcNow;
            run.UpdatedAt = run.CompletedAtUtc;
            await _unitOfWork.SaveChangesAsync(ct);

            return new BidOpsSupplierMatchExecutionResult(
                run.SupplierCount,
                run.MatchedCount,
                run.MissingEvidenceCount,
                "supplier match run completed");
        }
        catch (Exception ex)
        {
            run.Status = BidOpsSupplierMatchRunStatuses.Failed;
            run.ErrorMessage = Truncate(ex.Message, 2000);
            run.CompletedAtUtc = DateTime.UtcNow;
            run.UpdatedAt = run.CompletedAtUtc;
            await _unitOfWork.SaveChangesAsync(ct);
            throw;
        }
    }

    public async Task<GoNoGoDecisionDto> CreateDecisionAsync(
        long packageId,
        CreateGoNoGoDecisionRequest request,
        CancellationToken ct = default)
    {
        var tenantId = RequireTenantId();
        var userId = RequireUserId();
        _ = await GetPackageAsync(packageId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps package does not exist: {packageId}");

        if (request.MatchRunId.HasValue)
        {
            var run = await GetRunAsync(request.MatchRunId.Value, tracking: false, ct)
                ?? throw new AtlasException($"BidOps supplier match run does not exist: {request.MatchRunId.Value}");
            if (run.PackageId != packageId)
                throw new AtlasException("匹配运行不属于当前包件。");
        }

        if (request.SupplierMatchResultId.HasValue)
        {
            var result = await GetResultAsync(request.SupplierMatchResultId.Value, ct)
                ?? throw new AtlasException($"BidOps supplier match result does not exist: {request.SupplierMatchResultId.Value}");
            if (result.PackageId != packageId)
                throw new AtlasException("匹配结果不属于当前包件。");
        }

        if (request.OpportunityId.HasValue)
        {
            var opportunity = await GetOpportunityAsync(request.OpportunityId.Value, ct)
                ?? throw new AtlasException($"BidOps opportunity does not exist: {request.OpportunityId.Value}");
            if (opportunity.PackageId != packageId)
                throw new AtlasException("商机不属于当前包件。");
        }

        var now = DateTime.UtcNow;
        var decision = new GoNoGoDecision
        {
            Id = _idGenerator.NextId(),
            TenantId = tenantId,
            PackageId = packageId,
            OpportunityId = request.OpportunityId,
            MatchRunId = request.MatchRunId,
            SupplierMatchResultId = request.SupplierMatchResultId,
            SupplierId = request.SupplierId,
            Decision = NormalizeDecision(request.Decision),
            Reason = Truncate(request.Reason, 2000),
            RiskSummary = Truncate(request.RiskSummary, 2000),
            DecidedByUserId = userId,
            DecidedByUserName = Truncate(_current.UserName, 128),
            DecidedAtUtc = now,
            CreatedAt = now
        };

        await _decisions.AddAsync(decision, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapDecision(decision);
    }

    public async Task<IReadOnlyList<GoNoGoDecisionDto>> ListDecisionsAsync(
        long packageId,
        CancellationToken ct = default)
    {
        var package = await GetPackageAsync(packageId, tracking: false, ct);
        if (package == null)
            return [];

        var query = await _decisions.QueryDataScopeAsync(BidOpsDataResources.GoNoGoDecision, AtlasDataScopeType.AllTenant, ct);
        var decisions = await query
            .Where(x => x.PackageId == packageId)
            .OrderByDescending(x => x.DecidedAtUtc)
            .ToListAsync(ct);
        return decisions.Select(MapDecision).ToList();
    }

    private MatchResultDraft BuildMatchResult(
        SupplierMatchRun run,
        TenderPackage package,
        Notice? notice,
        IReadOnlyCollection<RequirementItem> requirements,
        Supplier supplier,
        IReadOnlyCollection<SupplierCapability> capabilities,
        IReadOnlyCollection<SupplierEvidenceDocument> evidenceDocuments,
        DateTime now)
    {
        var score = 20m;
        var riskFlags = new List<string>();
        var categoryMatched = IsCategoryMatched(package.Category, capabilities);
        if (categoryMatched)
            score += 25m;
        else if (!string.IsNullOrWhiteSpace(package.Category))
        {
            score += capabilities.Count > 0 ? 8m : 0m;
            riskFlags.Add("CategoryMismatch");
        }
        else
        {
            score += 15m;
        }

        var regionMatched = IsRegionMatched(notice?.Region, package.DeliveryPlace, supplier, capabilities);
        score += regionMatched ? 10m : 3m;
        if (!regionMatched)
            riskFlags.Add("RegionMismatch");

        score += supplier.QualityScore.HasValue
            ? Math.Clamp(supplier.QualityScore.Value, 0m, 100m) / 10m
            : 5m;

        var evidenceRequirements = requirements
            .Where(x => !string.IsNullOrWhiteSpace(x.RequiredEvidenceType))
            .ToList();
        var validEvidenceCount = 0;
        var expiringEvidenceCount = 0;
        var missingOrExpiredCount = 0;
        var mandatoryMissingCount = 0;
        var checks = new List<MissingEvidenceCheck>();
        var resultId = _idGenerator.NextId();

        foreach (var requirement in evidenceRequirements)
        {
            var match = FindEvidenceMatch(requirement.RequiredEvidenceType, evidenceDocuments);
            if (match == null)
            {
                missingOrExpiredCount++;
                if (requirement.IsMandatory)
                    mandatoryMissingCount++;
                checks.Add(CreateMissingEvidenceCheck(
                    run,
                    resultId,
                    supplier.Id,
                    requirement,
                    null,
                    BidOpsMissingEvidenceStatuses.Missing,
                    "未找到匹配的厂家资质材料。"));
                continue;
            }

            if (match.Status == BidOpsSupplierEvidenceStatuses.Expired ||
                (match.ValidTo.HasValue && match.ValidTo.Value < now))
            {
                missingOrExpiredCount++;
                if (requirement.IsMandatory)
                    mandatoryMissingCount++;
                checks.Add(CreateMissingEvidenceCheck(
                    run,
                    resultId,
                    supplier.Id,
                    requirement,
                    match.Id,
                    BidOpsMissingEvidenceStatuses.Expired,
                    "匹配到的厂家资质材料已过期。"));
                continue;
            }

            if (match.Status == BidOpsSupplierEvidenceStatuses.ExpiringSoon ||
                (match.ValidTo.HasValue && match.ValidTo.Value <= now.AddDays(30)))
            {
                expiringEvidenceCount++;
                checks.Add(CreateMissingEvidenceCheck(
                    run,
                    resultId,
                    supplier.Id,
                    requirement,
                    match.Id,
                    BidOpsMissingEvidenceStatuses.ExpiringSoon,
                    "匹配到的厂家资质材料即将到期，需要人工确认。"));
            }

            validEvidenceCount++;
        }

        if (evidenceRequirements.Count == 0)
        {
            score += 25m;
        }
        else
        {
            score += Math.Round(30m * validEvidenceCount / evidenceRequirements.Count, 2);
        }

        if (missingOrExpiredCount > 0)
            riskFlags.Add("MissingEvidence");
        if (expiringEvidenceCount > 0)
            riskFlags.Add("ExpiringSoonEvidence");
        if (mandatoryMissingCount > 0)
            riskFlags.Add("MandatoryRequirementGap");

        score = Math.Clamp(score, 0m, 100m);
        var recommendation = score >= 70m && missingOrExpiredCount == 0 && mandatoryMissingCount == 0
            ? BidOpsSupplierMatchRecommendations.Candidate
            : score >= 50m
                ? BidOpsSupplierMatchRecommendations.Caution
                : BidOpsSupplierMatchRecommendations.NotRecommended;
        var matchLevel = score >= 75m && missingOrExpiredCount == 0
            ? BidOpsSupplierMatchLevels.High
            : score >= 50m
                ? BidOpsSupplierMatchLevels.Medium
                : BidOpsSupplierMatchLevels.Low;

        var result = new SupplierMatchResult
        {
            Id = resultId,
            TenantId = run.TenantId,
            RunId = run.Id,
            PackageId = run.PackageId,
            SupplierId = supplier.Id,
            SupplierNameSnapshot = BuildSupplierNameSnapshot(supplier),
            Score = score,
            MatchLevel = matchLevel,
            Recommendation = recommendation,
            CategoryMatched = categoryMatched,
            RegionMatched = regionMatched,
            EvidenceMatchedCount = validEvidenceCount,
            MissingEvidenceCount = missingOrExpiredCount,
            RiskFlags = Truncate(string.Join(',', riskFlags.Distinct()), 1000),
            Explanation = BuildExplanation(categoryMatched, regionMatched, validEvidenceCount, evidenceRequirements.Count, missingOrExpiredCount, score),
            CreatedAt = now
        };

        return new MatchResultDraft(result, checks);
    }

    private MissingEvidenceCheck CreateMissingEvidenceCheck(
        SupplierMatchRun run,
        long resultId,
        long supplierId,
        RequirementItem requirement,
        long? matchedEvidenceDocumentId,
        string status,
        string explanation)
    {
        return new MissingEvidenceCheck
        {
            Id = _idGenerator.NextId(),
            TenantId = run.TenantId,
            RunId = run.Id,
            ResultId = resultId,
            PackageId = run.PackageId,
            SupplierId = supplierId,
            RequirementId = requirement.Id,
            MatchedEvidenceDocumentId = matchedEvidenceDocumentId,
            RequiredEvidenceType = Truncate(requirement.RequiredEvidenceType, 128),
            RequirementText = Truncate(requirement.OriginalText, 1000),
            Status = status,
            Explanation = Truncate(explanation, 1000),
            CreatedAt = DateTime.UtcNow
        };
    }

    private async Task<List<SupplierMatchResultDto>> ListRunResultsCoreAsync(long runId, CancellationToken ct)
    {
        var resultQuery = await _results.QueryDataScopeAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct);
        var results = await resultQuery
            .Where(x => x.RunId == runId)
            .OrderBy(x => x.Rank)
            .ToListAsync(ct);
        if (results.Count == 0)
            return [];

        var resultIds = results.Select(x => x.Id).ToArray();
        var checkQuery = await _missingEvidenceChecks.QueryDataScopeAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct);
        var checks = await checkQuery
            .Where(x => resultIds.Contains(x.ResultId))
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        var checksByResult = checks
            .GroupBy(x => x.ResultId)
            .ToDictionary(x => x.Key, x => x.Select(MapMissingEvidenceCheck).ToList());

        return results.Select(x => MapResult(x, checksByResult.GetValueOrDefault(x.Id) ?? [])).ToList();
    }

    private async Task<SupplierMatchRun?> GetRunAsync(long runId, bool tracking, CancellationToken ct)
    {
        var query = tracking
            ? await _runs.QueryDataScopeTrackingAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct)
            : await _runs.QueryDataScopeAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == runId).FirstOrDefaultAsync(ct);
    }

    private async Task<SupplierMatchResult?> GetResultAsync(long resultId, CancellationToken ct)
    {
        var query = await _results.QueryDataScopeAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == resultId).FirstOrDefaultAsync(ct);
    }

    private async Task<TenderPackage?> GetPackageAsync(long packageId, bool tracking, CancellationToken ct)
    {
        var query = tracking
            ? await _packages.QueryDataScopeTrackingAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct)
            : await _packages.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == packageId).FirstOrDefaultAsync(ct);
    }

    private async Task<Notice?> GetNoticeAsync(long noticeId, CancellationToken ct)
    {
        var query = await _notices.QueryDataScopeAsync(BidOpsDataResources.Notice, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == noticeId).FirstOrDefaultAsync(ct);
    }

    private async Task<Opportunity?> GetOpportunityAsync(long opportunityId, CancellationToken ct)
    {
        var query = await _opportunities.QueryDataScopeAsync(BidOpsDataResources.Opportunity, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => x.Id == opportunityId).FirstOrDefaultAsync(ct);
    }

    private async Task<List<RequirementItem>> ListRequirementsAsync(long packageId, CancellationToken ct)
    {
        var query = await _requirements.QueryDataScopeAsync(BidOpsDataResources.TenderPackage, AtlasDataScopeType.AllTenant, ct);
        return await query
            .Where(x => x.PackageId == packageId)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
    }

    private async Task<List<Supplier>> ListActiveSuppliersAsync(int maxSuppliers, CancellationToken ct)
    {
        var query = await _suppliers.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        var suppliers = await query
            .Where(x => x.Status == BidOpsSupplierStatuses.Active)
            .OrderByDescending(x => x.QualityScore ?? 0)
            .Take(maxSuppliers)
            .ToListAsync(ct);
        return suppliers
            .OrderByDescending(x => x.QualityScore ?? 0)
            .ThenBy(x => x.Name)
            .ToList();
    }

    private async Task<List<SupplierCapability>> ListCapabilitiesAsync(long[] supplierIds, CancellationToken ct)
    {
        if (supplierIds.Length == 0)
            return [];

        var query = await _capabilities.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        return await query.Where(x => supplierIds.Contains(x.SupplierId)).ToListAsync(ct);
    }

    private async Task<List<SupplierEvidenceDocument>> ListEvidenceDocumentsAsync(long[] supplierIds, CancellationToken ct)
    {
        if (supplierIds.Length == 0)
            return [];

        var query = await _evidenceDocuments.QueryDataScopeAsync(BidOpsDataResources.SupplierEvidence, AtlasDataScopeType.AllTenant, ct);
        return await query
            .Where(x => supplierIds.Contains(x.SupplierId) && x.Status != BidOpsSupplierEvidenceStatuses.Archived)
            .ToListAsync(ct);
    }

    private long RequireTenantId()
    {
        return _current.TenantId ?? throw new AtlasException("BidOps matching requires tenant context.");
    }

    private long RequireUserId()
    {
        return _current.UserId ?? throw new AtlasException("BidOps matching requires authenticated user context.");
    }

    private static SupplierEvidenceDocument? FindEvidenceMatch(
        string requiredEvidenceType,
        IReadOnlyCollection<SupplierEvidenceDocument> evidenceDocuments)
    {
        return evidenceDocuments
            .Where(x =>
                TextMatches(x.DocumentType, requiredEvidenceType) ||
                TextMatches(x.DocumentName, requiredEvidenceType))
            .OrderByDescending(x => x.Status == BidOpsSupplierEvidenceStatuses.Valid)
            .ThenByDescending(x => x.ValidTo)
            .FirstOrDefault();
    }

    private static bool IsCategoryMatched(
        string packageCategory,
        IReadOnlyCollection<SupplierCapability> capabilities)
    {
        if (string.IsNullOrWhiteSpace(packageCategory))
            return true;

        return capabilities.Any(x =>
            TextMatches(x.Category, packageCategory) ||
            TextMatches(x.ProductLine, packageCategory) ||
            TextMatches(x.CapabilityTags, packageCategory));
    }

    private static bool IsRegionMatched(
        string? noticeRegion,
        string? deliveryPlace,
        Supplier supplier,
        IReadOnlyCollection<SupplierCapability> capabilities)
    {
        var region = string.IsNullOrWhiteSpace(noticeRegion) ? deliveryPlace : noticeRegion;
        if (string.IsNullOrWhiteSpace(region) || string.IsNullOrWhiteSpace(supplier.Region))
            return true;

        return TextMatches(supplier.Region, region) ||
               capabilities.Any(x => TextMatches(x.RegionScope, region));
    }

    private static bool TextMatches(string? source, string? target)
    {
        if (string.IsNullOrWhiteSpace(source) || string.IsNullOrWhiteSpace(target))
            return false;

        var left = source.Trim();
        var right = target.Trim();
        return left.Contains(right, StringComparison.OrdinalIgnoreCase) ||
               right.Contains(left, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeDecision(string value)
    {
        return value?.Trim() switch
        {
            BidOpsGoNoGoDecisions.Go => BidOpsGoNoGoDecisions.Go,
            BidOpsGoNoGoDecisions.NoGo => BidOpsGoNoGoDecisions.NoGo,
            _ => BidOpsGoNoGoDecisions.Hold
        };
    }

    private static string BuildCriteriaSummary(TenderPackage package, string? userCriteria)
    {
        var pieces = new List<string>();
        if (!string.IsNullOrWhiteSpace(package.PackageName))
            pieces.Add($"包件：{package.PackageName}");
        if (!string.IsNullOrWhiteSpace(package.Category))
            pieces.Add($"品类：{package.Category}");
        if (!string.IsNullOrWhiteSpace(userCriteria))
            pieces.Add($"人工条件：{userCriteria.Trim()}");

        return pieces.Count == 0 ? $"包件 {package.Id} 厂家匹配" : Truncate(string.Join("；", pieces), 2000);
    }

    private static string BuildExplanation(
        bool categoryMatched,
        bool regionMatched,
        int evidenceMatchedCount,
        int evidenceRequirementCount,
        int missingEvidenceCount,
        decimal score)
    {
        var evidenceText = evidenceRequirementCount == 0
            ? "公告未识别到明确资质材料要求"
            : $"资质命中 {evidenceMatchedCount}/{evidenceRequirementCount}";
        var missingText = missingEvidenceCount > 0 ? $"，缺失或过期 {missingEvidenceCount} 项" : string.Empty;
        return Truncate(
            $"品类{(categoryMatched ? "匹配" : "不匹配")}，地区{(regionMatched ? "匹配" : "需确认")}，{evidenceText}{missingText}，综合分 {score:0.##}。",
            2000);
    }

    private static SupplierMatchRunDto MapRun(SupplierMatchRun run)
    {
        return new SupplierMatchRunDto
        {
            Id = run.Id,
            PackageId = run.PackageId,
            BackgroundJobId = run.BackgroundJobId,
            RunNo = run.RunNo,
            Status = run.Status,
            RequestedByUserId = run.RequestedByUserId,
            RequestedByUserName = run.RequestedByUserName,
            CriteriaSummary = run.CriteriaSummary,
            MaxSuppliers = run.MaxSuppliers,
            SupplierCount = run.SupplierCount,
            MatchedCount = run.MatchedCount,
            MissingEvidenceCount = run.MissingEvidenceCount,
            StartedAtUtc = run.StartedAtUtc,
            CompletedAtUtc = run.CompletedAtUtc,
            ErrorMessage = run.ErrorMessage,
            CreatedAt = run.CreatedAt,
            UpdatedAt = run.UpdatedAt
        };
    }

    private static SupplierMatchResultDto MapResult(
        SupplierMatchResult result,
        List<MissingEvidenceCheckDto> checks)
    {
        return new SupplierMatchResultDto
        {
            Id = result.Id,
            RunId = result.RunId,
            PackageId = result.PackageId,
            SupplierId = result.SupplierId,
            SupplierNameSnapshot = result.SupplierNameSnapshot,
            Rank = result.Rank,
            Score = result.Score,
            MatchLevel = result.MatchLevel,
            Recommendation = result.Recommendation,
            CategoryMatched = result.CategoryMatched,
            RegionMatched = result.RegionMatched,
            EvidenceMatchedCount = result.EvidenceMatchedCount,
            MissingEvidenceCount = result.MissingEvidenceCount,
            RiskFlags = result.RiskFlags,
            Explanation = result.Explanation,
            MissingEvidenceChecks = checks
        };
    }

    private static MissingEvidenceCheckDto MapMissingEvidenceCheck(MissingEvidenceCheck check)
    {
        return new MissingEvidenceCheckDto
        {
            Id = check.Id,
            RunId = check.RunId,
            ResultId = check.ResultId,
            PackageId = check.PackageId,
            SupplierId = check.SupplierId,
            RequirementId = check.RequirementId,
            MatchedEvidenceDocumentId = check.MatchedEvidenceDocumentId,
            RequiredEvidenceType = check.RequiredEvidenceType,
            RequirementText = check.RequirementText,
            Status = check.Status,
            Explanation = check.Explanation
        };
    }

    private static GoNoGoDecisionDto MapDecision(GoNoGoDecision decision)
    {
        return new GoNoGoDecisionDto
        {
            Id = decision.Id,
            PackageId = decision.PackageId,
            OpportunityId = decision.OpportunityId,
            MatchRunId = decision.MatchRunId,
            SupplierMatchResultId = decision.SupplierMatchResultId,
            SupplierId = decision.SupplierId,
            Decision = decision.Decision,
            Reason = decision.Reason,
            RiskSummary = decision.RiskSummary,
            DecidedByUserId = decision.DecidedByUserId,
            DecidedByUserName = decision.DecidedByUserName,
            DecidedAtUtc = decision.DecidedAtUtc
        };
    }

    private static TenderPackageDto MapPackage(
        TenderPackage package,
        Notice? notice,
        IReadOnlyCollection<RequirementItem> requirements)
    {
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
            RequirementCount = requirements.Count,
            RejectRiskCount = requirements.Count(x => x.IsRejectRisk),
            CreatedAt = package.CreatedAt,
            UpdatedAt = package.UpdatedAt
        };
    }

    private static RequirementItemDto MapRequirement(RequirementItem requirement)
    {
        return new RequirementItemDto
        {
            Id = requirement.Id,
            PackageId = requirement.PackageId,
            RequirementType = requirement.RequirementType,
            OriginalText = requirement.OriginalText,
            IsMandatory = requirement.IsMandatory,
            IsRejectRisk = requirement.IsRejectRisk,
            RequiredEvidenceType = requirement.RequiredEvidenceType,
            RiskLevel = requirement.RiskLevel
        };
    }

    private static (int PageIndex, int PageSize) NormalizePaging(BidOpsPagedQuery query)
    {
        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        return (pageIndex, pageSize);
    }

    private static string Truncate(string? value, int maxLength)
    {
        value = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string BuildSupplierNameSnapshot(Supplier supplier)
    {
        var name = Truncate(supplier.Name, 300);
        return string.IsNullOrWhiteSpace(name)
            ? Truncate($"待补录厂家-{Math.Abs(supplier.Id % 1_000_000):D6}", 300)
            : name;
    }

    private sealed record MatchResultDraft(
        SupplierMatchResult Result,
        List<MissingEvidenceCheck> MissingEvidenceChecks);
}
