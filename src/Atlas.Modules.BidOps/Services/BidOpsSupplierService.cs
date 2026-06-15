using Atlas.BackgroundTasks;
using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Core.IdGenerators;
using Atlas.Core.Services;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps;
using Atlas.Models.Tenant.Responses;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Matching;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Pursuits;
using Atlas.Modules.BidOps.Entities.Suppliers;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsSupplierService : IBidOpsSupplierService
{
    private const int DefaultPageSize = 20;
    private const int MaxPageSize = 200;

    private readonly IRepository<Supplier> _suppliers;
    private readonly IRepository<SupplierContact> _contacts;
    private readonly IRepository<SupplierCapability> _capabilities;
    private readonly IRepository<SupplierEvidenceDocument> _evidenceDocuments;
    private readonly IRepository<SupplierMatchResult> _matchResults;
    private readonly IRepository<GoNoGoDecision> _decisions;
    private readonly IRepository<Pursuit> _pursuits;
    private readonly IRepository<OutcomeSupplierRecord> _outcomeRecords;
    private readonly IRepository<TenderPackage> _packages;
    private readonly IRepository<Notice> _notices;
    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IBackgroundJobClient _jobs;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ICurrentIdentity _current;
    private readonly IIdGenerator _idGenerator;

    public BidOpsSupplierService(
        IRepository<Supplier> suppliers,
        IRepository<SupplierContact> contacts,
        IRepository<SupplierCapability> capabilities,
        IRepository<SupplierEvidenceDocument> evidenceDocuments,
        IRepository<SupplierMatchResult> matchResults,
        IRepository<GoNoGoDecision> decisions,
        IRepository<Pursuit> pursuits,
        IRepository<OutcomeSupplierRecord> outcomeRecords,
        IRepository<TenderPackage> packages,
        IRepository<Notice> notices,
        IRepository<RawNotice> rawNotices,
        IBackgroundJobClient jobs,
        IUnitOfWork unitOfWork,
        ICurrentIdentity current,
        IIdGenerator idGenerator)
    {
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _contacts = contacts ?? throw new ArgumentNullException(nameof(contacts));
        _capabilities = capabilities ?? throw new ArgumentNullException(nameof(capabilities));
        _evidenceDocuments = evidenceDocuments ?? throw new ArgumentNullException(nameof(evidenceDocuments));
        _matchResults = matchResults ?? throw new ArgumentNullException(nameof(matchResults));
        _decisions = decisions ?? throw new ArgumentNullException(nameof(decisions));
        _pursuits = pursuits ?? throw new ArgumentNullException(nameof(pursuits));
        _outcomeRecords = outcomeRecords ?? throw new ArgumentNullException(nameof(outcomeRecords));
        _packages = packages ?? throw new ArgumentNullException(nameof(packages));
        _notices = notices ?? throw new ArgumentNullException(nameof(notices));
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _jobs = jobs ?? throw new ArgumentNullException(nameof(jobs));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _current = current ?? throw new ArgumentNullException(nameof(current));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
    }

    public async Task<PagedResult<SupplierDto>> SearchAsync(
        SupplierSearchQuery query,
        CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _suppliers.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.Name.Contains(keyword) ||
                x.SupplierNo.Contains(keyword) ||
                x.UnifiedSocialCreditCode.Contains(keyword) ||
                x.ContactName.Contains(keyword) ||
                x.ContactPhone.Contains(keyword));
        }

        if (!string.IsNullOrWhiteSpace(query.Status))
        {
            var status = query.Status.Trim();
            builder = builder.Where(x => x.Status == status);
        }

        if (!string.IsNullOrWhiteSpace(query.Region))
        {
            var region = query.Region.Trim();
            builder = builder.Where(x => x.Region.Contains(region));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var supplierIds = await FindSupplierIdsByCategoryAsync(query.Category.Trim(), ct);
            if (supplierIds.Count == 0)
            {
                return new PagedResult<SupplierDto>(0, [], pageIndex, pageSize);
            }

            builder = builder.Where(x => supplierIds.Contains(x.Id));
        }

        if (query.EvidenceExpiringOnly == true)
        {
            var supplierIds = await FindSupplierIdsWithExpiringEvidenceAsync(DateTime.UtcNow.AddDays(30), ct);
            if (supplierIds.Count == 0)
            {
                return new PagedResult<SupplierDto>(0, [], pageIndex, pageSize);
            }

            builder = builder.Where(x => supplierIds.Contains(x.Id));
        }

        var total = await builder.CountAsync(ct);
        var suppliers = await builder
            .OrderByDescending(x => x.UpdatedAt ?? x.CreatedAt)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<SupplierDto>(
            total,
            await MapSuppliersAsync(suppliers, ct),
            pageIndex,
            pageSize);
    }

    public async Task<SupplierDetailDto?> GetAsync(long id, CancellationToken ct = default)
    {
        var supplier = await GetSupplierAsync(id, tracking: false, ct);
        if (supplier == null)
            return null;

        var contactQuery = await _contacts.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        var contacts = await contactQuery
            .Where(x => x.SupplierId == id)
            .OrderByDescending(x => x.IsPrimary)
            .ToListAsync(ct);

        var capabilityQuery = await _capabilities.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        var capabilities = await capabilityQuery
            .Where(x => x.SupplierId == id)
            .OrderBy(x => x.Category)
            .ToListAsync(ct);

        var evidenceQuery = await _evidenceDocuments.QueryDataScopeAsync(BidOpsDataResources.SupplierEvidence, AtlasDataScopeType.AllTenant, ct);
        var evidenceDocuments = await evidenceQuery
            .Where(x => x.SupplierId == id)
            .OrderBy(x => x.ValidTo)
            .ToListAsync(ct);

        return new SupplierDetailDto
        {
            Supplier = (await MapSuppliersAsync(new[] { supplier }, ct)).Single(),
            Contacts = contacts.Select(MapContact).ToList(),
            Capabilities = capabilities.Select(MapCapability).ToList(),
            EvidenceDocuments = evidenceDocuments.Select(MapEvidenceDocument).ToList()
        };
    }

    public async Task<SupplierAnalysisSummaryDto> GetAnalysisSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var supplierQuery = await _suppliers.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        var suppliers = await supplierQuery.ToListAsync(ct);

        var summary = new SupplierAnalysisSummaryDto
        {
            GeneratedAtUtc = now,
            SupplierSourceDescription = "厂家主档来自 BidOps 厂家能力库；公开中标/候选公示会保守同步采购方/厂家主数据，但不会自动写入厂家能力或资质证明。",
            OutcomeExtractionStatus = "已接入公开结果公示厂家线索抽取：线索保留来源公告、证据文本和包件快照，用于历史中标厂家分析与人工跟进判断。",
            TotalSuppliers = suppliers.Count,
            ActiveSuppliers = suppliers.Count(x => x.Status == BidOpsSupplierStatuses.Active),
            InactiveSuppliers = suppliers.Count(x => x.Status == BidOpsSupplierStatuses.Inactive),
            BlockedSuppliers = suppliers.Count(x => x.Status == BidOpsSupplierStatuses.Blocked)
        };

        var outcomeQuery = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        var outcomes = await outcomeQuery.ToListAsync(ct);
        ApplyOutcomeSummary(summary, outcomes);

        if (suppliers.Count == 0)
            return summary;

        var supplierIds = suppliers.Select(x => x.Id).ToHashSet();
        var capabilityQuery = await _capabilities.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        var capabilities = await capabilityQuery.Where(x => supplierIds.Contains(x.SupplierId)).ToListAsync(ct);
        var evidenceQuery = await _evidenceDocuments.QueryDataScopeAsync(BidOpsDataResources.SupplierEvidence, AtlasDataScopeType.AllTenant, ct);
        var evidenceDocuments = await evidenceQuery.Where(x => supplierIds.Contains(x.SupplierId)).ToListAsync(ct);
        var matchResultQuery = await _matchResults.QueryDataScopeAsync(BidOpsDataResources.Matching, AtlasDataScopeType.AllTenant, ct);
        var matchResults = await matchResultQuery.Where(x => supplierIds.Contains(x.SupplierId)).ToListAsync(ct);
        var decisionQuery = await _decisions.QueryDataScopeAsync(BidOpsDataResources.GoNoGoDecision, AtlasDataScopeType.AllTenant, ct);
        var decisions = await decisionQuery.Where(x => x.SupplierId.HasValue).ToListAsync(ct);
        decisions = decisions.Where(x => supplierIds.Contains(x.SupplierId!.Value)).ToList();
        var pursuitQuery = await _pursuits.QueryDataScopeAsync(BidOpsDataResources.Pursuit, AtlasDataScopeType.AllTenant, ct);
        var pursuits = await pursuitQuery.Where(x => x.SupplierId.HasValue).ToListAsync(ct);
        pursuits = pursuits.Where(x => supplierIds.Contains(x.SupplierId!.Value)).ToList();

        var qualityScores = suppliers.Where(x => x.QualityScore.HasValue).Select(x => x.QualityScore!.Value).ToList();
        summary.AverageQualityScore = qualityScores.Count == 0 ? null : Math.Round(qualityScores.Average(), 2);
        summary.SuppliersWithCapabilities = capabilities.Select(x => x.SupplierId).Distinct().Count();
        summary.SuppliersWithEvidence = evidenceDocuments.Select(x => x.SupplierId).Distinct().Count();
        summary.ExpiringEvidenceDocuments = evidenceDocuments.Count(x => GetCurrentEvidenceStatus(x, now) == BidOpsSupplierEvidenceStatuses.ExpiringSoon);
        summary.ExpiredEvidenceDocuments = evidenceDocuments.Count(x => GetCurrentEvidenceStatus(x, now) == BidOpsSupplierEvidenceStatuses.Expired);
        summary.MatchedSupplierCount = matchResults.Select(x => x.SupplierId).Distinct().Count();
        summary.CandidateSupplierCount = matchResults
            .Where(x => x.Recommendation == BidOpsSupplierMatchRecommendations.Candidate)
            .Select(x => x.SupplierId)
            .Distinct()
            .Count();
        summary.GoDecisionCount = decisions.Count(x => x.Decision == BidOpsGoNoGoDecisions.Go);
        summary.PursuitSupplierCount = pursuits.Select(x => x.SupplierId!.Value).Distinct().Count();
        summary.CapabilityCategories = capabilities
            .GroupBy(x => NormalizeBucketCode(x.Category, "Other"))
            .Select(x => new SupplierAnalysisBucketDto
            {
                Code = x.Key,
                Count = x.Count(),
                SupplierCount = x.Select(item => item.SupplierId).Distinct().Count()
            })
            .OrderByDescending(x => x.SupplierCount)
            .ThenByDescending(x => x.Count)
            .ThenBy(x => x.Code)
            .ToList();

        summary.EvidenceStatuses = evidenceDocuments
            .GroupBy(x => GetCurrentEvidenceStatus(x, now))
            .Select(x => new SupplierAnalysisBucketDto
            {
                Code = x.Key,
                Count = x.Count(),
                SupplierCount = x.Select(item => item.SupplierId).Distinct().Count()
            })
            .OrderByDescending(x => x.Count)
            .ThenBy(x => x.Code)
            .ToList();

        var capabilitiesBySupplier = capabilities.GroupBy(x => x.SupplierId).ToDictionary(x => x.Key, x => x.ToList());
        var evidenceBySupplier = evidenceDocuments.GroupBy(x => x.SupplierId).ToDictionary(x => x.Key, x => x.ToList());
        var matchesBySupplier = matchResults.GroupBy(x => x.SupplierId).ToDictionary(x => x.Key, x => x.ToList());
        var decisionsBySupplier = decisions.GroupBy(x => x.SupplierId!.Value).ToDictionary(x => x.Key, x => x.ToList());
        var pursuitsBySupplier = pursuits.GroupBy(x => x.SupplierId!.Value).ToDictionary(x => x.Key, x => x.ToList());

        summary.TopSuppliers = suppliers
            .Select(x => MapSupplierAnalysisItem(
                x,
                now,
                capabilitiesBySupplier.GetValueOrDefault(x.Id) ?? [],
                evidenceBySupplier.GetValueOrDefault(x.Id) ?? [],
                matchesBySupplier.GetValueOrDefault(x.Id) ?? [],
                decisionsBySupplier.GetValueOrDefault(x.Id) ?? [],
                pursuitsBySupplier.GetValueOrDefault(x.Id) ?? []))
            .OrderByDescending(x => x.PursuitCount)
            .ThenByDescending(x => x.GoDecisionCount)
            .ThenByDescending(x => x.CandidateMatchCount)
            .ThenByDescending(x => x.MatchResultCount)
            .ThenByDescending(x => x.CapabilityCount)
            .ThenByDescending(x => x.EvidenceCount)
            .ThenByDescending(x => x.QualityScore ?? -1)
            .ThenBy(x => x.SupplierName)
            .Take(100)
            .ToList();

        return summary;
    }

    public async Task<PagedResult<OutcomeSupplierRecordDto>> SearchOutcomeRecordsAsync(
        OutcomeSupplierSearchQuery query,
        CancellationToken ct = default)
    {
        var (pageIndex, pageSize) = NormalizePaging(query);
        var builder = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);

        if (query.RawNoticeId.HasValue)
            builder = builder.Where(x => x.RawNoticeId == query.RawNoticeId.Value);
        if (query.PackageId.HasValue)
            builder = builder.Where(x => x.TenderPackageId == query.PackageId.Value);
        if (query.SupplierId.HasValue)
            builder = builder.Where(x => x.SupplierId == query.SupplierId.Value);
        if (!string.IsNullOrWhiteSpace(query.OutcomeType))
        {
            var outcomeType = query.OutcomeType.Trim();
            builder = builder.Where(x => x.OutcomeType == outcomeType);
        }

        if (!string.IsNullOrWhiteSpace(query.SupplierName))
        {
            var supplierName = query.SupplierName.Trim();
            builder = builder.Where(x => x.SupplierName.Contains(supplierName));
        }

        if (!string.IsNullOrWhiteSpace(query.PackageNo))
        {
            var packageNo = query.PackageNo.Trim();
            builder = builder.Where(x => x.PackageNo.Contains(packageNo) || x.LotNo.Contains(packageNo));
        }

        if (!string.IsNullOrWhiteSpace(query.Category))
        {
            var category = query.Category.Trim();
            builder = builder.Where(x => x.Category.Contains(category));
        }

        if (query.HasAwardAmount == true)
            builder = builder.Where(x => x.AwardAmount.HasValue && x.AwardAmount.Value > 0);
        else if (query.HasAwardAmount == false)
            builder = builder.Where(x => !x.AwardAmount.HasValue || x.AwardAmount.Value <= 0);

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var keyword = query.Keyword.Trim();
            builder = builder.Where(x =>
                x.SupplierName.Contains(keyword) ||
                x.ProjectName.Contains(keyword) ||
                x.ProjectCode.Contains(keyword) ||
                x.PackageName.Contains(keyword) ||
                x.PackageNo.Contains(keyword) ||
                x.NoticeTitle.Contains(keyword) ||
                x.EvidenceText.Contains(keyword));
        }

        var total = await builder.CountAsync(ct);
        var records = await ApplyOutcomeRecordSort(builder, query.SortBy)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedResult<OutcomeSupplierRecordDto>(
            total,
            records.Select(MapOutcomeRecord).ToList(),
            pageIndex,
            pageSize);
    }

    private static IQueryBuilder<OutcomeSupplierRecord> ApplyOutcomeRecordSort(
        IQueryBuilder<OutcomeSupplierRecord> query,
        string? sortBy)
    {
        return sortBy?.Trim() switch
        {
            "AwardAmountDesc" => query.OrderByDescending(x => x.AwardAmount ?? 0m),
            "AwardAmountAsc" => query.OrderBy(x => x.AwardAmount ?? 0m),
            _ => query.OrderByDescending(x => x.PublishTime ?? x.CreatedAt)
        };
    }

    public async Task<SupplierOutcomeSummaryDto> GetOutcomeSummaryAsync(CancellationToken ct = default)
    {
        var builder = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);
        var records = await builder.ToListAsync(ct);
        return BuildOutcomeSummary(records);
    }

    public async Task<IReadOnlyList<PackageHistoricalSupplierLeadDto>> ListHistoricalSupplierLeadsAsync(
        long packageId,
        CancellationToken ct = default)
    {
        var packageQuery = await _packages.QueryDataScopeAsync(
            BidOpsDataResources.TenderPackage,
            AtlasDataScopeType.AllTenant,
            ct);
        var package = await packageQuery.Where(x => x.Id == packageId).FirstOrDefaultAsync(ct);
        if (package == null)
            return [];

        var noticeQuery = await _notices.QueryDataScopeAsync(
            BidOpsDataResources.Notice,
            AtlasDataScopeType.AllTenant,
            ct);
        var notice = await noticeQuery.Where(x => x.Id == package.NoticeId).FirstOrDefaultAsync(ct);
        var keywords = ExtractPackageKeywords(package).ToList();
        var primaryKeyword = keywords.FirstOrDefault() ?? string.Empty;
        var category = BidOpsTextQuality.CleanExtractedValue(package.Category);
        var packageNo = BidOpsTextQuality.CleanExtractedValue(package.PackageNo);
        var lotNo = BidOpsTextQuality.CleanExtractedValue(package.LotNo);

        if (string.IsNullOrWhiteSpace(category) &&
            string.IsNullOrWhiteSpace(packageNo) &&
            string.IsNullOrWhiteSpace(lotNo) &&
            string.IsNullOrWhiteSpace(primaryKeyword))
        {
            return [];
        }

        var builder = await _outcomeRecords.QueryDataScopeAsync(
            BidOpsDataResources.OutcomeSupplierRecord,
            AtlasDataScopeType.AllTenant,
            ct);

        if (!string.IsNullOrWhiteSpace(packageNo) || !string.IsNullOrWhiteSpace(lotNo))
        {
            builder = builder.Where(x =>
                (!string.IsNullOrEmpty(packageNo) && x.PackageNo == packageNo) ||
                (!string.IsNullOrEmpty(lotNo) && x.LotNo == lotNo) ||
                (!string.IsNullOrEmpty(primaryKeyword) && x.PackageName.Contains(primaryKeyword)));
        }
        else if (!string.IsNullOrWhiteSpace(category))
        {
            builder = builder.Where(x =>
                x.Category.Contains(category) ||
                (!string.IsNullOrEmpty(primaryKeyword) && x.PackageName.Contains(primaryKeyword)));
        }
        else
        {
            builder = builder.Where(x => x.PackageName.Contains(primaryKeyword) || x.ProjectName.Contains(primaryKeyword));
        }

        var candidates = await builder
            .OrderByDescending(x => x.PublishTime ?? x.CreatedAt)
            .Take(500)
            .ToListAsync(ct);

        return candidates
            .Select(x => BuildHistoricalLead(x, package, notice, keywords))
            .Where(x => x.MatchScore > 0)
            .GroupBy(x => new
            {
                Supplier = x.SupplierName,
                x.ProjectCode,
                x.PackageNo,
                x.OutcomeType,
                x.Rank
            })
            .Select(x => x.OrderByDescending(item => item.MatchScore).ThenByDescending(item => item.PublishTime).First())
            .OrderByDescending(x => x.MatchScore)
            .ThenByDescending(x => x.OutcomeType == BidOpsOutcomeTypes.Awarded)
            .ThenByDescending(x => x.PublishTime)
            .Take(50)
            .ToList();
    }

    public async Task<OutcomeSupplierBackfillEnqueueDto> EnqueueOutcomeSupplierBackfillAsync(
        int maxItems,
        CancellationToken ct = default)
    {
        var tenant = _current.TenantId
            ?? throw new AtlasException("Tenant context is required for BidOps operations.");
        var userId = _current.UserId
            ?? throw new AtlasException("Authenticated user context is required for BidOps operations.");
        var limit = Math.Clamp(maxItems <= 0 ? 200 : maxItems, 1, 500);

        var rawQuery = await _rawNotices.QueryDataScopeAsync(
            BidOpsDataResources.RawNotice,
            AtlasDataScopeType.AllTenant,
            ct);
        var raws = await rawQuery
            .Where(x =>
                x.NoticeType.Contains("Award") ||
                x.NoticeType.Contains("Candidate") ||
                x.Title.Contains("中标") ||
                x.Title.Contains("成交") ||
                x.Title.Contains("候选") ||
                x.TextPreview.Contains("中标人") ||
                x.TextPreview.Contains("成交供应商") ||
                x.TextPreview.Contains("候选人"))
            .OrderByDescending(x => x.PublishTime ?? x.FetchTime)
            .Take(limit)
            .ToListAsync(ct);

        var response = new OutcomeSupplierBackfillEnqueueDto
        {
            RequestedMaxItems = limit
        };
        var backfillRunId = _idGenerator.NextId();

        foreach (var raw in raws)
        {
            var result = await _jobs.EnqueueAsync(
                new EnqueueBackgroundJobRequest<OutcomeSupplierExtractJobPayload>
                {
                    JobType = BidOpsBackgroundJobTypes.OutcomeSupplierExtract,
                    Queue = BidOpsBackgroundJobQueues.BidOps,
                    JobName = "BidOps outcome supplier extract",
                    TenantId = tenant,
                    StoreId = _current.StoreId,
                    DeduplicationKey = $"bidops:outcome-supplier-extract:{tenant}:{raw.Id}:{backfillRunId}",
                    MaxAttempts = 3,
                    Payload = new OutcomeSupplierExtractJobPayload(
                        tenant,
                        _current.StoreId,
                        userId,
                        _current.UserName,
                        raw.Id)
                },
                ct);

            response.Jobs.Add(new EnqueueJobDto(result.JobId, result.JobType, result.Queue, result.AlreadyExists));
        }

        response.QueuedCount = response.Jobs.Count;
        return response;
    }

    public async Task<SupplierDto> CreateAsync(
        CreateSupplierRequest request,
        CancellationToken ct = default)
    {
        var tenantId = _current.TenantId ?? throw new AtlasException("BidOps supplier requires tenant context.");
        var name = CleanRequired(request.Name, "供应商名称");

        var now = DateTime.UtcNow;
        var id = _idGenerator.NextId();
        var supplier = new Supplier
        {
            Id = id,
            TenantId = tenantId,
            SupplierNo = $"SUP-{now:yyyyMMdd}-{Math.Abs(id % 1_000_000):D6}",
            Name = Truncate(name, 300),
            UnifiedSocialCreditCode = Truncate(request.UnifiedSocialCreditCode, 64),
            Region = Truncate(request.Region, 128),
            Address = Truncate(request.Address, 500),
            ContactName = Truncate(request.ContactName, 128),
            ContactPhone = Truncate(request.ContactPhone, 64),
            ContactEmail = Truncate(request.ContactEmail, 256),
            Status = BidOpsSupplierStatuses.Active,
            QualityScore = NormalizeScore(request.QualityScore),
            Remark = Truncate(request.Remark, 1000),
            CreatedAt = now
        };

        await _suppliers.AddAsync(supplier, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return (await MapSuppliersAsync(new[] { supplier }, ct)).Single();
    }

    public async Task<SupplierDto> UpdateAsync(
        long id,
        UpdateSupplierRequest request,
        CancellationToken ct = default)
    {
        var supplier = await GetSupplierAsync(id, tracking: true, ct)
            ?? throw new AtlasException($"BidOps supplier does not exist: {id}");

        if (request.Name != null)
            supplier.Name = Truncate(CleanRequired(request.Name, "供应商名称"), 300);
        if (request.UnifiedSocialCreditCode != null)
            supplier.UnifiedSocialCreditCode = Truncate(request.UnifiedSocialCreditCode, 64);
        if (request.Region != null)
            supplier.Region = Truncate(request.Region, 128);
        if (request.Address != null)
            supplier.Address = Truncate(request.Address, 500);
        if (request.ContactName != null)
            supplier.ContactName = Truncate(request.ContactName, 128);
        if (request.ContactPhone != null)
            supplier.ContactPhone = Truncate(request.ContactPhone, 64);
        if (request.ContactEmail != null)
            supplier.ContactEmail = Truncate(request.ContactEmail, 256);
        if (!string.IsNullOrWhiteSpace(request.Status))
            supplier.Status = NormalizeStatus(request.Status);
        if (request.QualityScore.HasValue)
            supplier.QualityScore = NormalizeScore(request.QualityScore);
        if (request.Remark != null)
            supplier.Remark = Truncate(request.Remark, 1000);

        supplier.UpdatedAt = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync(ct);
        return (await MapSuppliersAsync(new[] { supplier }, ct)).Single();
    }

    public async Task<SupplierContactDto> AddContactAsync(
        long supplierId,
        CreateSupplierContactRequest request,
        CancellationToken ct = default)
    {
        var supplier = await GetSupplierAsync(supplierId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps supplier does not exist: {supplierId}");
        var name = CleanRequired(request.Name, "联系人姓名");

        var now = DateTime.UtcNow;
        var contact = new SupplierContact
        {
            Id = _idGenerator.NextId(),
            TenantId = supplier.TenantId,
            SupplierId = supplierId,
            Name = Truncate(name, 128),
            Role = Truncate(request.Role, 128),
            Phone = Truncate(request.Phone, 64),
            Email = Truncate(request.Email, 256),
            IsPrimary = request.IsPrimary,
            Remark = Truncate(request.Remark, 500),
            CreatedAt = now
        };

        await _contacts.AddAsync(contact, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapContact(contact);
    }

    public async Task<SupplierCapabilityDto> AddCapabilityAsync(
        long supplierId,
        CreateSupplierCapabilityRequest request,
        CancellationToken ct = default)
    {
        var supplier = await GetSupplierAsync(supplierId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps supplier does not exist: {supplierId}");
        var category = CleanRequired(request.Category, "能力分类");

        var capability = new SupplierCapability
        {
            Id = _idGenerator.NextId(),
            TenantId = supplier.TenantId,
            SupplierId = supplierId,
            Category = Truncate(category, 128),
            ProductLine = Truncate(request.ProductLine, 200),
            CapabilityTags = Truncate(request.CapabilityTags, 1000),
            RegionScope = Truncate(request.RegionScope, 300),
            QualificationLevel = Truncate(request.QualificationLevel, 128),
            Remark = Truncate(request.Remark, 500),
            CreatedAt = DateTime.UtcNow
        };

        await _capabilities.AddAsync(capability, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapCapability(capability);
    }

    public async Task<SupplierEvidenceDocumentDto> AddEvidenceDocumentAsync(
        long supplierId,
        CreateSupplierEvidenceDocumentRequest request,
        CancellationToken ct = default)
    {
        var supplier = await GetSupplierAsync(supplierId, tracking: false, ct)
            ?? throw new AtlasException($"BidOps supplier does not exist: {supplierId}");
        var documentName = CleanRequired(request.DocumentName, "材料名称");
        var documentType = CleanRequired(request.DocumentType, "材料类型");

        var evidence = new SupplierEvidenceDocument
        {
            Id = _idGenerator.NextId(),
            TenantId = supplier.TenantId,
            SupplierId = supplierId,
            DocumentName = Truncate(documentName, 300),
            DocumentType = Truncate(documentType, 128),
            EvidenceNo = Truncate(request.EvidenceNo, 128),
            IssuedBy = Truncate(request.IssuedBy, 300),
            ValidFrom = request.ValidFrom,
            ValidTo = request.ValidTo,
            FileName = Truncate(request.FileName, 300),
            FileUrl = Truncate(request.FileUrl, 1000),
            StorageProvider = Truncate(request.StorageProvider, 64),
            StorageKey = Truncate(request.StorageKey, 500),
            Status = CalculateEvidenceStatus(request.ValidTo, DateTime.UtcNow),
            Remark = Truncate(request.Remark, 500),
            CreatedAt = DateTime.UtcNow
        };

        await _evidenceDocuments.AddAsync(evidence, ct);
        await _unitOfWork.SaveChangesAsync(ct);
        return MapEvidenceDocument(evidence);
    }

    private async Task<Supplier?> GetSupplierAsync(long id, bool tracking, CancellationToken ct)
    {
        var builder = tracking
            ? await _suppliers.QueryDataScopeTrackingAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct)
            : await _suppliers.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        return await builder.Where(x => x.Id == id).FirstOrDefaultAsync(ct);
    }

    private async Task<List<SupplierDto>> MapSuppliersAsync(
        IReadOnlyCollection<Supplier> suppliers,
        CancellationToken ct)
    {
        if (suppliers.Count == 0)
            return [];

        var ids = suppliers.Select(x => x.Id).Distinct().ToArray();
        var now = DateTime.UtcNow;
        var contactQuery = await _contacts.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        var contacts = await contactQuery.Where(x => ids.Contains(x.SupplierId)).ToListAsync(ct);
        var capabilityQuery = await _capabilities.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        var capabilities = await capabilityQuery.Where(x => ids.Contains(x.SupplierId)).ToListAsync(ct);
        var evidenceQuery = await _evidenceDocuments.QueryDataScopeAsync(BidOpsDataResources.SupplierEvidence, AtlasDataScopeType.AllTenant, ct);
        var evidenceDocuments = await evidenceQuery.Where(x => ids.Contains(x.SupplierId)).ToListAsync(ct);

        var contactCounts = contacts.GroupBy(x => x.SupplierId).ToDictionary(x => x.Key, x => x.Count());
        var capabilityCounts = capabilities.GroupBy(x => x.SupplierId).ToDictionary(x => x.Key, x => x.Count());
        var evidenceCounts = evidenceDocuments.GroupBy(x => x.SupplierId).ToDictionary(x => x.Key, x => x.Count());
        var expiringCounts = evidenceDocuments
            .Where(x => IsEvidenceExpiring(x, now.AddDays(30)))
            .GroupBy(x => x.SupplierId)
            .ToDictionary(x => x.Key, x => x.Count());

        return suppliers.Select(x => new SupplierDto
        {
            Id = x.Id,
            SupplierNo = x.SupplierNo,
            Name = x.Name,
            UnifiedSocialCreditCode = x.UnifiedSocialCreditCode,
            Region = x.Region,
            Address = x.Address,
            ContactName = x.ContactName,
            ContactPhone = x.ContactPhone,
            ContactEmail = x.ContactEmail,
            Status = x.Status,
            QualityScore = x.QualityScore,
            Remark = x.Remark,
            ContactCount = contactCounts.GetValueOrDefault(x.Id),
            CapabilityCount = capabilityCounts.GetValueOrDefault(x.Id),
            EvidenceCount = evidenceCounts.GetValueOrDefault(x.Id),
            ExpiringEvidenceCount = expiringCounts.GetValueOrDefault(x.Id),
            CreatedAt = x.CreatedAt,
            UpdatedAt = x.UpdatedAt
        }).ToList();
    }

    private async Task<HashSet<long>> FindSupplierIdsByCategoryAsync(string category, CancellationToken ct)
    {
        var query = await _capabilities.QueryDataScopeAsync(BidOpsDataResources.Supplier, AtlasDataScopeType.AllTenant, ct);
        var capabilities = await query
            .Where(x => x.Category.Contains(category) || x.ProductLine.Contains(category) || x.CapabilityTags.Contains(category))
            .ToListAsync(ct);
        return capabilities.Select(x => x.SupplierId).ToHashSet();
    }

    private async Task<HashSet<long>> FindSupplierIdsWithExpiringEvidenceAsync(DateTime warningUntil, CancellationToken ct)
    {
        var query = await _evidenceDocuments.QueryDataScopeAsync(BidOpsDataResources.SupplierEvidence, AtlasDataScopeType.AllTenant, ct);
        var documents = await query
            .Where(x =>
                x.ValidTo.HasValue &&
                x.ValidTo.Value <= warningUntil &&
                x.Status != BidOpsSupplierEvidenceStatuses.Archived)
            .ToListAsync(ct);
        return documents.Select(x => x.SupplierId).ToHashSet();
    }

    private static SupplierContactDto MapContact(SupplierContact contact)
    {
        return new SupplierContactDto
        {
            Id = contact.Id,
            SupplierId = contact.SupplierId,
            Name = contact.Name,
            Role = contact.Role,
            Phone = contact.Phone,
            Email = contact.Email,
            IsPrimary = contact.IsPrimary,
            Remark = contact.Remark
        };
    }

    private static SupplierCapabilityDto MapCapability(SupplierCapability capability)
    {
        return new SupplierCapabilityDto
        {
            Id = capability.Id,
            SupplierId = capability.SupplierId,
            Category = capability.Category,
            ProductLine = capability.ProductLine,
            CapabilityTags = capability.CapabilityTags,
            RegionScope = capability.RegionScope,
            QualificationLevel = capability.QualificationLevel,
            Remark = capability.Remark
        };
    }

    private static SupplierEvidenceDocumentDto MapEvidenceDocument(SupplierEvidenceDocument document)
    {
        return new SupplierEvidenceDocumentDto
        {
            Id = document.Id,
            SupplierId = document.SupplierId,
            DocumentName = document.DocumentName,
            DocumentType = document.DocumentType,
            EvidenceNo = document.EvidenceNo,
            IssuedBy = document.IssuedBy,
            ValidFrom = document.ValidFrom,
            ValidTo = document.ValidTo,
            FileName = document.FileName,
            FileUrl = document.FileUrl,
            Status = document.Status,
            Remark = document.Remark
        };
    }

    private static SupplierAnalysisItemDto MapSupplierAnalysisItem(
        Supplier supplier,
        DateTime now,
        IReadOnlyCollection<SupplierCapability> capabilities,
        IReadOnlyCollection<SupplierEvidenceDocument> evidenceDocuments,
        IReadOnlyCollection<SupplierMatchResult> matchResults,
        IReadOnlyCollection<GoNoGoDecision> decisions,
        IReadOnlyCollection<Pursuit> pursuits)
    {
        var evidenceStatuses = evidenceDocuments.Select(x => GetCurrentEvidenceStatus(x, now)).ToList();
        return new SupplierAnalysisItemDto
        {
            SupplierId = supplier.Id,
            SupplierNo = supplier.SupplierNo,
            SupplierName = supplier.Name,
            Status = supplier.Status,
            Region = supplier.Region,
            QualityScore = supplier.QualityScore,
            CapabilityCount = capabilities.Count,
            EvidenceCount = evidenceDocuments.Count,
            ValidEvidenceCount = evidenceStatuses.Count(x => x == BidOpsSupplierEvidenceStatuses.Valid),
            ExpiringEvidenceCount = evidenceStatuses.Count(x => x == BidOpsSupplierEvidenceStatuses.ExpiringSoon),
            ExpiredEvidenceCount = evidenceStatuses.Count(x => x == BidOpsSupplierEvidenceStatuses.Expired),
            MatchResultCount = matchResults.Count,
            CandidateMatchCount = matchResults.Count(x => x.Recommendation == BidOpsSupplierMatchRecommendations.Candidate),
            CautionMatchCount = matchResults.Count(x => x.Recommendation == BidOpsSupplierMatchRecommendations.Caution),
            NotRecommendedMatchCount = matchResults.Count(x => x.Recommendation == BidOpsSupplierMatchRecommendations.NotRecommended),
            GoDecisionCount = decisions.Count(x => x.Decision == BidOpsGoNoGoDecisions.Go),
            NoGoDecisionCount = decisions.Count(x => x.Decision == BidOpsGoNoGoDecisions.NoGo),
            HoldDecisionCount = decisions.Count(x => x.Decision == BidOpsGoNoGoDecisions.Hold),
            PursuitCount = pursuits.Count,
            LastMatchedAtUtc = matchResults.Count == 0 ? null : matchResults.Max(x => x.CreatedAt),
            LastDecisionAtUtc = decisions.Count == 0 ? null : decisions.Max(x => x.DecidedAtUtc),
            LastPursuitCreatedAtUtc = pursuits.Count == 0 ? null : pursuits.Max(x => x.CreatedAt),
            RiskFlags = BuildRiskFlags(matchResults)
        };
    }

    private static OutcomeSupplierRecordDto MapOutcomeRecord(OutcomeSupplierRecord record)
    {
        return new OutcomeSupplierRecordDto
        {
            Id = record.Id,
            RawNoticeId = record.RawNoticeId,
            NoticeId = record.NoticeId,
            TenderPackageId = record.TenderPackageId,
            BuyerId = record.BuyerId,
            SupplierId = record.SupplierId,
            SourceUrl = record.SourceUrl,
            NoticeTitle = record.NoticeTitle,
            NoticeType = record.NoticeType,
            ProjectName = record.ProjectName,
            ProjectCode = record.ProjectCode,
            BuyerName = record.BuyerName,
            Region = record.Region,
            PublishTime = record.PublishTime,
            LotNo = record.LotNo,
            LotName = record.LotName,
            PackageNo = record.PackageNo,
            PackageName = record.PackageName,
            Category = record.Category,
            SupplierName = record.SupplierName,
            OutcomeType = record.OutcomeType,
            Rank = record.Rank,
            AwardAmount = record.AwardAmount,
            ProcurementAgencyServiceFeeAmount = record.ProcurementAgencyServiceFeeAmount,
            Currency = record.Currency,
            EvidenceText = record.EvidenceText,
            ExtractionConfidence = record.ExtractionConfidence,
            CreatedAt = record.CreatedAt
        };
    }

    private static void ApplyOutcomeSummary(
        SupplierAnalysisSummaryDto summary,
        IReadOnlyCollection<OutcomeSupplierRecord> records)
    {
        var outcomeSummary = BuildOutcomeSummary(records);
        summary.OutcomeRecordCount = outcomeSummary.RecordCount;
        summary.OutcomeSupplierCount = outcomeSummary.SupplierCount;
        summary.AwardedOutcomeCount = outcomeSummary.AwardedCount;
        summary.CandidateOutcomeCount = outcomeSummary.CandidateCount;
        summary.LinkedOutcomeSupplierCount = outcomeSummary.LinkedSupplierCount;
        summary.TopOutcomeSuppliers = outcomeSummary.TopSuppliers;
    }

    private static SupplierOutcomeSummaryDto BuildOutcomeSummary(IReadOnlyCollection<OutcomeSupplierRecord> records)
    {
        var topSuppliers = records
            .GroupBy(x => string.IsNullOrWhiteSpace(x.SupplierNameNormalized) ? x.SupplierName : x.SupplierNameNormalized)
            .Select(x =>
            {
                var latest = x
                    .OrderByDescending(item => item.PublishTime ?? item.CreatedAt)
                    .First();

                return new SupplierOutcomeStatDto
                {
                    SupplierName = latest.SupplierName,
                    SupplierId = latest.SupplierId,
                    OutcomeCount = x.Count(),
                    AwardedCount = x.Count(item => item.OutcomeType == BidOpsOutcomeTypes.Awarded),
                    CandidateCount = x.Count(item => item.OutcomeType == BidOpsOutcomeTypes.Candidate),
                    TotalAwardAmount = x.Where(item => item.AwardAmount.HasValue).Sum(item => item.AwardAmount),
                    LastPublishTime = x.Max(item => item.PublishTime ?? item.CreatedAt)
                };
            })
            .OrderByDescending(x => (x.TotalAwardAmount ?? 0m) > 0m)
            .ThenByDescending(x => x.TotalAwardAmount ?? 0m)
            .ThenByDescending(x => x.AwardedCount)
            .ThenByDescending(x => x.OutcomeCount)
            .ThenBy(x => x.SupplierName)
            .Take(50)
            .ToList();

        return new SupplierOutcomeSummaryDto
        {
            GeneratedAtUtc = DateTime.UtcNow,
            RecordCount = records.Count,
            SupplierCount = records
                .Select(x => string.IsNullOrWhiteSpace(x.SupplierNameNormalized) ? x.SupplierName : x.SupplierNameNormalized)
                .Where(x => !string.IsNullOrWhiteSpace(x))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .Count(),
            AwardedCount = records.Count(x => x.OutcomeType == BidOpsOutcomeTypes.Awarded),
            CandidateCount = records.Count(x => x.OutcomeType == BidOpsOutcomeTypes.Candidate),
            LinkedPackageCount = records.Where(x => x.TenderPackageId.HasValue).Select(x => x.TenderPackageId!.Value).Distinct().Count(),
            LinkedSupplierCount = records.Where(x => x.SupplierId.HasValue).Select(x => x.SupplierId!.Value).Distinct().Count(),
            TopSuppliers = topSuppliers
        };
    }

    private static IEnumerable<string> ExtractPackageKeywords(TenderPackage package)
    {
        var source = string.Join(
            " ",
            BidOpsTextQuality.CleanExtractedValue(package.Category),
            BidOpsTextQuality.CleanExtractedValue(package.PackageName),
            BidOpsTextQuality.CleanExtractedValue(package.LotName));

        var separators = new[] { ' ', ',', '，', ';', '；', '、', '/', '\\', '-', '_', '(', ')', '（', '）', '[', ']', '【', '】' };
        return source
            .Split(separators, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(x => x.Length >= 2)
            .Where(x => !BidOpsTextQuality.IsUnknownMarker(x))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8);
    }

    private static PackageHistoricalSupplierLeadDto BuildHistoricalLead(
        OutcomeSupplierRecord record,
        TenderPackage package,
        Notice? notice,
        IReadOnlyCollection<string> keywords)
    {
        var reasons = new List<string>();
        var score = 0m;

        if (HasMeaningfulEqual(package.PackageNo, record.PackageNo))
        {
            score += 0.35m;
            reasons.Add("包件号相同");
        }

        if (HasMeaningfulEqual(package.LotNo, record.LotNo))
        {
            score += 0.25m;
            reasons.Add("标段号相同");
        }

        if (HasMeaningfulOverlap(package.Category, record.Category))
        {
            score += 0.35m;
            reasons.Add("品类相近");
        }

        var matchedKeyword = keywords.FirstOrDefault(keyword =>
            record.PackageName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            record.ProjectName.Contains(keyword, StringComparison.OrdinalIgnoreCase) ||
            record.EvidenceText.Contains(keyword, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(matchedKeyword))
        {
            score += 0.2m;
            reasons.Add($"名称关键词：{matchedKeyword}");
        }

        if (!string.IsNullOrWhiteSpace(notice?.ProjectCode) &&
            string.Equals(notice.ProjectCode, record.ProjectCode, StringComparison.OrdinalIgnoreCase))
        {
            score += 0.1m;
            reasons.Add("同项目编号");
        }

        if (record.OutcomeType == BidOpsOutcomeTypes.Awarded)
            score += 0.05m;

        return new PackageHistoricalSupplierLeadDto
        {
            OutcomeRecordId = record.Id,
            RawNoticeId = record.RawNoticeId,
            SupplierId = record.SupplierId,
            SupplierName = record.SupplierName,
            OutcomeType = record.OutcomeType,
            Rank = record.Rank,
            AwardAmount = record.AwardAmount,
            ProcurementAgencyServiceFeeAmount = record.ProcurementAgencyServiceFeeAmount,
            Currency = record.Currency,
            ProjectName = record.ProjectName,
            ProjectCode = record.ProjectCode,
            NoticeTitle = record.NoticeTitle,
            SourceUrl = record.SourceUrl,
            PublishTime = record.PublishTime,
            PackageNo = record.PackageNo,
            PackageName = record.PackageName,
            Category = record.Category,
            MatchReason = reasons.Count == 0 ? "公开结果公示历史线索" : string.Join("、", reasons.Distinct()),
            MatchScore = Math.Min(score, 1m),
            EvidenceText = record.EvidenceText
        };
    }

    private static bool HasMeaningfulEqual(string? left, string? right)
    {
        var leftClean = NormalizeComparable(left);
        var rightClean = NormalizeComparable(right);
        return !string.IsNullOrWhiteSpace(leftClean) &&
               !string.IsNullOrWhiteSpace(rightClean) &&
               leftClean == rightClean;
    }

    private static bool HasMeaningfulOverlap(string? left, string? right)
    {
        var leftClean = BidOpsTextQuality.CleanExtractedValue(left);
        var rightClean = BidOpsTextQuality.CleanExtractedValue(right);
        if (string.IsNullOrWhiteSpace(leftClean) || string.IsNullOrWhiteSpace(rightClean))
            return false;

        return leftClean.Contains(rightClean, StringComparison.OrdinalIgnoreCase) ||
               rightClean.Contains(leftClean, StringComparison.OrdinalIgnoreCase);
    }

    private static string NormalizeComparable(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned) || BidOpsTextQuality.IsUnknownMarker(cleaned))
            return string.Empty;

        return new string(cleaned
            .Where(x => !char.IsWhiteSpace(x) && !":：,，;；".Contains(x))
            .ToArray())
            .ToUpperInvariant();
    }

    private static (int PageIndex, int PageSize) NormalizePaging(BidOpsPagedQuery query)
    {
        var pageIndex = query.PageIndex <= 0 ? 1 : query.PageIndex;
        var pageSize = query.PageSize <= 0 ? DefaultPageSize : Math.Min(query.PageSize, MaxPageSize);
        return (pageIndex, pageSize);
    }

    private static string NormalizeStatus(string? value)
    {
        return value?.Trim() switch
        {
            BidOpsSupplierStatuses.Inactive => BidOpsSupplierStatuses.Inactive,
            BidOpsSupplierStatuses.Blocked => BidOpsSupplierStatuses.Blocked,
            _ => BidOpsSupplierStatuses.Active
        };
    }

    private static decimal? NormalizeScore(decimal? value)
    {
        return value.HasValue ? Math.Clamp(value.Value, 0, 100) : null;
    }

    private static string CalculateEvidenceStatus(DateTime? validTo, DateTime now)
    {
        if (!validTo.HasValue)
            return BidOpsSupplierEvidenceStatuses.Valid;
        if (validTo.Value < now)
            return BidOpsSupplierEvidenceStatuses.Expired;
        if (validTo.Value <= now.AddDays(30))
            return BidOpsSupplierEvidenceStatuses.ExpiringSoon;
        return BidOpsSupplierEvidenceStatuses.Valid;
    }

    private static string GetCurrentEvidenceStatus(SupplierEvidenceDocument document, DateTime now)
    {
        if (document.Status == BidOpsSupplierEvidenceStatuses.Archived)
            return BidOpsSupplierEvidenceStatuses.Archived;

        return CalculateEvidenceStatus(document.ValidTo, now);
    }

    private static bool IsEvidenceExpiring(SupplierEvidenceDocument document, DateTime warningUntil)
    {
        return document.Status != BidOpsSupplierEvidenceStatuses.Archived &&
               document.ValidTo.HasValue &&
               document.ValidTo.Value <= warningUntil;
    }

    private static string NormalizeBucketCode(string? value, string fallback)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return string.IsNullOrWhiteSpace(cleaned) ? fallback : cleaned.Trim();
    }

    private static string BuildRiskFlags(IEnumerable<SupplierMatchResult> matchResults)
    {
        var flags = matchResults
            .SelectMany(x => SplitRiskFlags(x.RiskFlags))
            .Where(x => !string.Equals(x, "None", StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();

        return flags.Count == 0 ? string.Empty : string.Join("、", flags);
    }

    private static IEnumerable<string> SplitRiskFlags(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            yield break;

        var parts = cleaned.Split(
            [',', ';', '|', '/', '\\', '，', '；', '、', '\n', '\r'],
            StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var part in parts)
        {
            if (!string.IsNullOrWhiteSpace(part))
                yield return part;
        }
    }

    private static string Truncate(string? value, int maxLength)
    {
        value = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        return value.Length <= maxLength ? value : value[..maxLength];
    }

    private static string CleanRequired(string? value, string fieldName)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            throw new AtlasException($"{fieldName}不能为空或全为乱码占位符。");

        return cleaned;
    }
}
