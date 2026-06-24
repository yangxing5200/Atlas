using System.Security.Cryptography;
using System.Text;
using Atlas.Core.IdGenerators;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Entities.Buyers;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Suppliers;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsOrganizationMasterDataService : IBidOpsOrganizationMasterDataService
{
    private readonly IRepository<Buyer> _buyers;
    private readonly IRepository<BuyerProcurementRecord> _buyerProcurements;
    private readonly IRepository<Supplier> _suppliers;
    private readonly IIdGenerator _idGenerator;
    private readonly ILogger<BidOpsOrganizationMasterDataService> _logger;

    public BidOpsOrganizationMasterDataService(
        IRepository<Buyer> buyers,
        IRepository<BuyerProcurementRecord> buyerProcurements,
        IRepository<Supplier> suppliers,
        IIdGenerator idGenerator,
        ILogger<BidOpsOrganizationMasterDataService> logger)
    {
        _buyers = buyers ?? throw new ArgumentNullException(nameof(buyers));
        _buyerProcurements = buyerProcurements ?? throw new ArgumentNullException(nameof(buyerProcurements));
        _suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        _idGenerator = idGenerator ?? throw new ArgumentNullException(nameof(idGenerator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BidOpsOrganizationMasterDataSyncResult> SyncOutcomeOrganizationsAsync(
        long tenantId,
        IReadOnlyList<OutcomeSupplierRecord> records,
        CancellationToken ct = default)
    {
        if (records.Count == 0)
            return new BidOpsOrganizationMasterDataSyncResult();

        var buyerResult = await UpsertBuyersAsync(tenantId, records, ct);
        var supplierResult = await UpsertSuppliersAsync(tenantId, records, ct);

        _logger.LogInformation(
            "BidOps synced outcome organizations. tenant={TenantId}; buyers created={BuyerCreated}, updated={BuyerUpdated}; suppliers created={SupplierCreated}, updated={SupplierUpdated}.",
            tenantId,
            buyerResult.Created,
            buyerResult.Updated,
            supplierResult.Created,
            supplierResult.Updated);

        return new BidOpsOrganizationMasterDataSyncResult
        {
            BuyerCreatedCount = buyerResult.Created,
            BuyerUpdatedCount = buyerResult.Updated,
            SupplierCreatedCount = supplierResult.Created,
            SupplierUpdatedCount = supplierResult.Updated
        };
    }

    public async Task<BidOpsOrganizationMasterDataSyncResult> SyncApprovedNoticeOrganizationsAsync(
        long tenantId,
        Notice notice,
        string sourceUrl,
        IReadOnlyList<TenderPackage> packages,
        IReadOnlyList<OutcomeSupplierRecord> outcomeRecords,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(notice);

        foreach (var record in outcomeRecords)
            ApplyApprovedNoticeContext(record, notice, sourceUrl, packages);

        var buyerSyncRecords = BuildBuyerSyncRecords(notice, sourceUrl, outcomeRecords);
        var buyerResult = await UpsertBuyersAsync(tenantId, buyerSyncRecords, ct);
        var supplierResult = await UpsertSuppliersAsync(tenantId, outcomeRecords, ct);
        var buyerId = ResolveBuyerId(notice.BuyerName, buyerSyncRecords);
        await UpsertBuyerProcurementAsync(tenantId, notice, sourceUrl, packages, buyerId, ct);

        _logger.LogInformation(
            "BidOps synced approved notice organizations. tenant={TenantId}; notice={NoticeId}; buyers created={BuyerCreated}, updated={BuyerUpdated}; suppliers created={SupplierCreated}, updated={SupplierUpdated}.",
            tenantId,
            notice.Id,
            buyerResult.Created,
            buyerResult.Updated,
            supplierResult.Created,
            supplierResult.Updated);

        return new BidOpsOrganizationMasterDataSyncResult
        {
            BuyerCreatedCount = buyerResult.Created,
            BuyerUpdatedCount = buyerResult.Updated,
            SupplierCreatedCount = supplierResult.Created,
            SupplierUpdatedCount = supplierResult.Updated
        };
    }

    private async Task<(int Created, int Updated)> UpsertBuyersAsync(
        long tenantId,
        IReadOnlyList<OutcomeSupplierRecord> records,
        CancellationToken ct)
    {
        var buyerNames = records
            .Select(x => BidOpsOrganizationNameNormalizer.Clean(x.BuyerName))
            .Where(IsUsableOrganizationName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (buyerNames.Length == 0)
            return (0, 0);

        var query = await _buyers.QueryTrackingAsync(tenantId, ct);
        var existing = await query.ToListAsync(ct);
        var map = existing
            .Where(x => !string.IsNullOrWhiteSpace(x.NameNormalized))
            .GroupBy(x => x.NameNormalized, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First(), StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        foreach (var name in buyerNames)
        {
            var normalized = BidOpsOrganizationNameNormalizer.NormalizeForMatch(name);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            var latest = records
                .Where(x => string.Equals(BidOpsOrganizationNameNormalizer.NormalizeForMatch(x.BuyerName), normalized, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.PublishTime ?? x.CreatedAt)
                .First();

            if (!map.TryGetValue(normalized, out var buyer))
            {
                var id = _idGenerator.NextId();
                var now = DateTime.UtcNow;
                buyer = new Buyer
                {
                    Id = id,
                    TenantId = tenantId,
                    BuyerNo = BidOpsBusinessNumberBuilder.Build("BUY", id, now),
                    Name = Truncate(name, 300),
                    NameNormalized = Truncate(normalized, 191),
                    Region = Truncate(latest.Region, 128),
                    SourceUrl = Truncate(latest.SourceUrl, 1500),
                    LastProjectCode = Truncate(latest.ProjectCode, 128),
                    LastProjectName = Truncate(latest.ProjectName, 500),
                    LastNoticeTitle = Truncate(latest.NoticeTitle, 500),
                    LastSeenAtUtc = latest.PublishTime ?? latest.CreatedAt,
                    Status = BidOpsBuyerStatuses.Active,
                    Remark = "Auto-created from public BidOps outcome/candidate notice.",
                    CreatedAt = now
                };
                await _buyers.AddAsync(buyer, tenantId, ct);
                map[normalized] = buyer;
                created++;
            }
            else if (ApplyBuyerObservedFields(buyer, latest))
            {
                updated++;
            }

            foreach (var record in records.Where(x => string.Equals(BidOpsOrganizationNameNormalizer.NormalizeForMatch(x.BuyerName), normalized, StringComparison.OrdinalIgnoreCase)))
                record.BuyerId = buyer.Id;
        }

        return (created, updated);
    }

    private async Task<(int Created, int Updated)> UpsertSuppliersAsync(
        long tenantId,
        IReadOnlyList<OutcomeSupplierRecord> records,
        CancellationToken ct)
    {
        var supplierNames = records
            .Select(x => BidOpsOrganizationNameNormalizer.Clean(x.SupplierName))
            .Where(IsUsableOrganizationName)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();
        if (supplierNames.Length == 0)
            return (0, 0);

        var query = await _suppliers.QueryTrackingAsync(tenantId, ct);
        var existing = await query.ToListAsync(ct);
        var map = existing
            .Select(x => new { Normalized = BidOpsOrganizationNameNormalizer.NormalizeForMatch(x.Name), Supplier = x })
            .Where(x => !string.IsNullOrWhiteSpace(x.Normalized))
            .GroupBy(x => x.Normalized, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(x => x.Key, x => x.First().Supplier, StringComparer.OrdinalIgnoreCase);

        var created = 0;
        var updated = 0;
        foreach (var name in supplierNames)
        {
            var normalized = BidOpsOrganizationNameNormalizer.NormalizeForMatch(name);
            if (string.IsNullOrWhiteSpace(normalized))
                continue;

            var latest = records
                .Where(x => string.Equals(BidOpsOrganizationNameNormalizer.NormalizeForMatch(x.SupplierName), normalized, StringComparison.OrdinalIgnoreCase))
                .OrderByDescending(x => x.PublishTime ?? x.CreatedAt)
                .First();

            if (!map.TryGetValue(normalized, out var supplier))
            {
                var id = _idGenerator.NextId();
                var now = DateTime.UtcNow;
                supplier = new Supplier
                {
                    Id = id,
                    TenantId = tenantId,
                    SupplierNo = BidOpsBusinessNumberBuilder.Build("SUP", id, now),
                    Name = Truncate(name, 300),
                    Region = Truncate(latest.Region, 128),
                    Status = BidOpsSupplierStatuses.Active,
                    Remark = "Auto-created from public BidOps outcome/candidate notice.",
                    CreatedFromRawNoticeId = latest.RawNoticeId,
                    CreatedFromNoticeId = latest.NoticeId,
                    CreatedFromNoticeTitle = Truncate(latest.NoticeTitle, 500),
                    CreatedFromSourceUrl = Truncate(latest.SourceUrl, 1500),
                    LastOutcomeRawNoticeId = latest.RawNoticeId,
                    LastOutcomeNoticeId = latest.NoticeId,
                    LastOutcomeNoticeTitle = Truncate(latest.NoticeTitle, 500),
                    LastOutcomeAtUtc = latest.PublishTime ?? latest.CreatedAt,
                    CreatedAt = now
                };
                await _suppliers.AddAsync(supplier, tenantId, ct);
                map[normalized] = supplier;
                created++;
            }
            else if (ApplySupplierObservedFields(supplier, latest))
            {
                updated++;
            }

            foreach (var record in records.Where(x => string.Equals(BidOpsOrganizationNameNormalizer.NormalizeForMatch(x.SupplierName), normalized, StringComparison.OrdinalIgnoreCase)))
                record.SupplierId = supplier.Id;
        }

        return (created, updated);
    }

    private static bool ApplyBuyerObservedFields(Buyer buyer, OutcomeSupplierRecord latest)
    {
        var changed = false;
        changed |= FillIfEmpty(value => buyer.Region = value, buyer.Region, latest.Region, 128);
        changed |= FillIfEmpty(value => buyer.SourceUrl = value, buyer.SourceUrl, latest.SourceUrl, 1500);
        changed |= SetIfChanged(value => buyer.LastProjectCode = value, buyer.LastProjectCode, latest.ProjectCode, 128);
        changed |= SetIfChanged(value => buyer.LastProjectName = value, buyer.LastProjectName, latest.ProjectName, 500);
        changed |= SetIfChanged(value => buyer.LastNoticeTitle = value, buyer.LastNoticeTitle, latest.NoticeTitle, 500);

        var seenAt = latest.PublishTime ?? latest.CreatedAt;
        if (!buyer.LastSeenAtUtc.HasValue || buyer.LastSeenAtUtc.Value < seenAt)
        {
            buyer.LastSeenAtUtc = seenAt;
            changed = true;
        }

        if (changed)
            buyer.UpdatedAt = DateTime.UtcNow;

        return changed;
    }

    private static bool ApplySupplierObservedFields(Supplier supplier, OutcomeSupplierRecord latest)
    {
        var changed = FillIfEmpty(value => supplier.Region = value, supplier.Region, latest.Region, 128);
        changed |= FillIfEmpty(value => supplier.CreatedFromNoticeTitle = value, supplier.CreatedFromNoticeTitle, latest.NoticeTitle, 500);
        changed |= FillIfEmpty(value => supplier.CreatedFromSourceUrl = value, supplier.CreatedFromSourceUrl, latest.SourceUrl, 1500);

        if (!supplier.CreatedFromRawNoticeId.HasValue)
        {
            supplier.CreatedFromRawNoticeId = latest.RawNoticeId;
            changed = true;
        }

        if (!supplier.CreatedFromNoticeId.HasValue && latest.NoticeId.HasValue)
        {
            supplier.CreatedFromNoticeId = latest.NoticeId;
            changed = true;
        }

        var outcomeAt = latest.PublishTime ?? latest.CreatedAt;
        if (!supplier.LastOutcomeAtUtc.HasValue || supplier.LastOutcomeAtUtc.Value <= outcomeAt)
        {
            supplier.LastOutcomeRawNoticeId = latest.RawNoticeId;
            supplier.LastOutcomeNoticeId = latest.NoticeId;
            supplier.LastOutcomeNoticeTitle = Truncate(latest.NoticeTitle, 500);
            supplier.LastOutcomeAtUtc = outcomeAt;
            changed = true;
        }

        if (changed)
            supplier.UpdatedAt = DateTime.UtcNow;

        return changed;
    }

    private async Task UpsertBuyerProcurementAsync(
        long tenantId,
        Notice notice,
        string sourceUrl,
        IReadOnlyList<TenderPackage> packages,
        long? buyerId,
        CancellationToken ct)
    {
        if (!buyerId.HasValue)
            return;

        var sourceHash = ComputeSourceHash(
            tenantId.ToString(),
            buyerId.Value.ToString(),
            notice.RawNoticeId.ToString(),
            notice.Id.ToString(),
            notice.ProjectCode,
            notice.Title);

        var query = await _buyerProcurements.QueryTrackingAsync(tenantId, ct);
        var existing = await query.Where(x => x.SourceHash == sourceHash).FirstOrDefaultAsync(ct);
        if (existing == null)
        {
            await _buyerProcurements.AddAsync(new BuyerProcurementRecord
            {
                Id = _idGenerator.NextId(),
                TenantId = tenantId,
                BuyerId = buyerId.Value,
                RawNoticeId = notice.RawNoticeId,
                NoticeId = notice.Id,
                SourceUrl = Truncate(sourceUrl, 1500),
                NoticeTitle = Truncate(notice.Title, 500),
                NoticeType = Truncate(notice.NoticeType, 64),
                ProjectName = Truncate(notice.ProjectName, 500),
                ProjectCode = Truncate(notice.ProjectCode, 128),
                Region = Truncate(notice.Region, 128),
                PublishTime = notice.PublishTime,
                BudgetAmount = notice.BudgetAmount,
                PackageCount = packages.Count,
                SourceHash = sourceHash,
                Remark = "Created when BidOps review approved this public notice.",
                CreatedAt = DateTime.UtcNow
            }, tenantId, ct);
            return;
        }

        var changed = false;
        changed |= SetIfChanged(value => existing.NoticeTitle = value, existing.NoticeTitle, notice.Title, 500);
        changed |= SetIfChanged(value => existing.NoticeType = value, existing.NoticeType, notice.NoticeType, 64);
        changed |= FillIfEmpty(value => existing.SourceUrl = value, existing.SourceUrl, sourceUrl, 1500);
        changed |= SetIfChanged(value => existing.ProjectName = value, existing.ProjectName, notice.ProjectName, 500);
        changed |= SetIfChanged(value => existing.ProjectCode = value, existing.ProjectCode, notice.ProjectCode, 128);
        changed |= SetIfChanged(value => existing.Region = value, existing.Region, notice.Region, 128);
        if (existing.NoticeId != notice.Id)
        {
            existing.NoticeId = notice.Id;
            changed = true;
        }

        if (existing.PublishTime != notice.PublishTime)
        {
            existing.PublishTime = notice.PublishTime;
            changed = true;
        }

        if (existing.BudgetAmount != notice.BudgetAmount)
        {
            existing.BudgetAmount = notice.BudgetAmount;
            changed = true;
        }

        if (existing.PackageCount != packages.Count)
        {
            existing.PackageCount = packages.Count;
            changed = true;
        }

        if (changed)
            existing.UpdatedAt = DateTime.UtcNow;
    }

    private static IReadOnlyList<OutcomeSupplierRecord> BuildBuyerSyncRecords(
        Notice notice,
        string sourceUrl,
        IReadOnlyList<OutcomeSupplierRecord> outcomeRecords)
    {
        if (string.IsNullOrWhiteSpace(notice.BuyerName))
            return outcomeRecords;

        var normalized = BidOpsOrganizationNameNormalizer.NormalizeForMatch(notice.BuyerName);
        var records = outcomeRecords.ToList();
        if (records.Any(x => string.Equals(BidOpsOrganizationNameNormalizer.NormalizeForMatch(x.BuyerName), normalized, StringComparison.OrdinalIgnoreCase)))
            return records;

        records.Add(new OutcomeSupplierRecord
        {
            TenantId = notice.TenantId,
            RawNoticeId = notice.RawNoticeId,
            NoticeId = notice.Id,
            NoticeTitle = notice.Title,
            NoticeType = notice.NoticeType,
            ProjectName = notice.ProjectName,
            ProjectCode = notice.ProjectCode,
            BuyerName = notice.BuyerName,
            Region = notice.Region,
            PublishTime = notice.PublishTime,
            SourceUrl = sourceUrl,
            SupplierName = "buyer-sync-placeholder",
            SupplierNameNormalized = "buyer-sync-placeholder",
            OutcomeType = BidOpsOutcomeTypes.Candidate,
            CreatedAt = notice.CreatedAt
        });
        return records;
    }

    private static long? ResolveBuyerId(string buyerName, IReadOnlyList<OutcomeSupplierRecord> records)
    {
        var normalized = BidOpsOrganizationNameNormalizer.NormalizeForMatch(buyerName);
        return records
            .FirstOrDefault(x => string.Equals(BidOpsOrganizationNameNormalizer.NormalizeForMatch(x.BuyerName), normalized, StringComparison.OrdinalIgnoreCase))
            ?.BuyerId;
    }

    private static void ApplyApprovedNoticeContext(
        OutcomeSupplierRecord record,
        Notice notice,
        string sourceUrl,
        IReadOnlyList<TenderPackage> packages)
    {
        record.NoticeId = notice.Id;
        record.NoticeTitle = string.IsNullOrWhiteSpace(record.NoticeTitle) ? Truncate(notice.Title, 500) : record.NoticeTitle;
        record.NoticeType = string.IsNullOrWhiteSpace(record.NoticeType) ? Truncate(notice.NoticeType, 64) : record.NoticeType;
        record.ProjectName = FirstMeaningful(record.ProjectName, notice.ProjectName);
        record.ProjectCode = FirstMeaningful(record.ProjectCode, notice.ProjectCode);
        record.BuyerName = FirstMeaningful(record.BuyerName, notice.BuyerName);
        record.Region = FirstMeaningful(record.Region, notice.Region);
        record.PublishTime ??= notice.PublishTime;
        record.SourceUrl = FirstMeaningful(record.SourceUrl, sourceUrl);

        var package = FindFormalPackage(record, packages);
        if (package != null)
        {
            record.TenderPackageId = package.Id;
            record.LotNo = FirstMeaningful(record.LotNo, package.LotNo);
            record.LotName = FirstMeaningful(record.LotName, package.LotName);
            record.PackageNo = FirstMeaningful(record.PackageNo, package.PackageNo);
            record.PackageName = FirstPackageNameDistinctFromProject(
                record.ProjectName,
                record.PackageName,
                package.PackageName);
            record.Category = FirstMeaningful(record.Category, package.Category);
        }
    }

    private static TenderPackage? FindFormalPackage(
        OutcomeSupplierRecord record,
        IReadOnlyList<TenderPackage> packages)
    {
        if (packages.Count == 0)
            return null;

        var packageNo = NormalizeCode(record.PackageNo);
        var lotNo = NormalizeCode(record.LotNo);
        var packageName = BidOpsTextQuality.CleanExtractedValue(record.PackageName);

        return packages.FirstOrDefault(x =>
                   (!string.IsNullOrWhiteSpace(packageNo) && NormalizeCode(x.PackageNo) == packageNo) ||
                   (!string.IsNullOrWhiteSpace(lotNo) && NormalizeCode(x.LotNo) == lotNo)) ??
               packages.FirstOrDefault(x =>
                   !string.IsNullOrWhiteSpace(packageName) &&
                   !string.IsNullOrWhiteSpace(x.PackageName) &&
                   (x.PackageName.Contains(packageName, StringComparison.OrdinalIgnoreCase) ||
                    packageName.Contains(x.PackageName, StringComparison.OrdinalIgnoreCase))) ??
               (packages.Count == 1 ? packages[0] : null);
    }

    private static bool FillIfEmpty(
        Action<string> assign,
        string existing,
        string candidate,
        int maxLength)
    {
        if (!string.IsNullOrWhiteSpace(BidOpsTextQuality.CleanExtractedValue(existing)))
            return false;

        var cleaned = Truncate(candidate, maxLength);
        if (string.IsNullOrWhiteSpace(cleaned))
            return false;

        assign(cleaned);
        return true;
    }

    private static bool SetIfChanged(
        Action<string> assign,
        string existing,
        string candidate,
        int maxLength)
    {
        var cleaned = Truncate(candidate, maxLength);
        if (string.IsNullOrWhiteSpace(cleaned) || string.Equals(existing, cleaned, StringComparison.Ordinal))
            return false;

        assign(cleaned);
        return true;
    }

    private static bool IsUsableOrganizationName(string value)
    {
        var normalized = BidOpsOrganizationNameNormalizer.NormalizeForMatch(value);
        return normalized.Length >= 4 &&
               !BidOpsTextQuality.IsUnknownMarker(value) &&
               !string.Equals(normalized, "无", StringComparison.OrdinalIgnoreCase) &&
               !string.Equals(normalized, "详见附件", StringComparison.OrdinalIgnoreCase);
    }

    private static string ComputeSourceHash(params string[] values)
    {
        var joined = string.Join('\u001f', values.Select(x => x ?? string.Empty));
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(joined));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static string FirstMeaningful(params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned) && !BidOpsTextQuality.IsUnknownMarker(cleaned))
                return cleaned;
        }

        return string.Empty;
    }

    private static string FirstPackageNameDistinctFromProject(string? projectName, params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned) &&
                !BidOpsTextQuality.IsUnknownMarker(cleaned) &&
                !IsSameMeaningfulValue(cleaned, projectName))
            {
                return cleaned;
            }
        }

        return string.Empty;
    }

    private static bool IsSameMeaningfulValue(string? left, string? right)
    {
        var normalizedLeft = NormalizeLooseText(left);
        var normalizedRight = NormalizeLooseText(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
    }

    private static string NormalizeCode(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned
                .Where(x => !char.IsWhiteSpace(x) && !":：,，;；".Contains(x))
                .ToArray())
            .ToUpperInvariant();
    }

    private static string NormalizeLooseText(string? value)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        if (string.IsNullOrWhiteSpace(cleaned))
            return string.Empty;

        return new string(cleaned
                .Where(x => !char.IsWhiteSpace(x) && !"　:：,，;；。.!！".Contains(x))
                .ToArray())
            .ToUpperInvariant();
    }

    private static string Truncate(string? value, int maxLength)
    {
        var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
        return cleaned.Length <= maxLength ? cleaned : cleaned[..maxLength];
    }
}
