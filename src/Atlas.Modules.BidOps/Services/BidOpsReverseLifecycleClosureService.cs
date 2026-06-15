using System.Text;
using Atlas.Core.Authorization;
using Atlas.Core.Exceptions;
using Atlas.Data.Abstractions;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Models;
using Microsoft.Extensions.Logging;

namespace Atlas.Modules.BidOps.Services;

public sealed class BidOpsReverseLifecycleClosureService : IBidOpsReverseLifecycleClosureService
{
    private const int RelatedNoticeScanLimit = 300;
    private const int StoredTextReadLimit = 300_000;

    private readonly IRepository<RawNotice> _rawNotices;
    private readonly IRepository<RawAttachment> _rawAttachments;
    private readonly IBidOpsFileStore _fileStore;
    private readonly IBidOpsCrawlService _crawl;
    private readonly BidOpsContentHasher _hasher;
    private readonly ILogger<BidOpsReverseLifecycleClosureService> _logger;

    public BidOpsReverseLifecycleClosureService(
        IRepository<RawNotice> rawNotices,
        IRepository<RawAttachment> rawAttachments,
        IBidOpsFileStore fileStore,
        IBidOpsCrawlService crawl,
        BidOpsContentHasher hasher,
        ILogger<BidOpsReverseLifecycleClosureService> logger)
    {
        _rawNotices = rawNotices ?? throw new ArgumentNullException(nameof(rawNotices));
        _rawAttachments = rawAttachments ?? throw new ArgumentNullException(nameof(rawAttachments));
        _fileStore = fileStore ?? throw new ArgumentNullException(nameof(fileStore));
        _crawl = crawl ?? throw new ArgumentNullException(nameof(crawl));
        _hasher = hasher ?? throw new ArgumentNullException(nameof(hasher));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<BidOpsReverseClosureDebugResult> ReverseCloseUrlAsync(
        BidOpsReverseCloseUrlRequest request,
        CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!Uri.TryCreate(request.Url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme is not ("http" or "https"))
            throw new AtlasException("A public http/https award notice URL is required.");

        var result = new BidOpsReverseClosureDebugResult
        {
            InputAwardNoticeUrl = uri.ToString()
        };

        if (request.ResetDerivedData)
        {
            result.Warnings.Add(
                "resetDerivedData is intentionally not executed by the debug API. Use tools/Atlas.LocalSetup reset-bidops-derived-data --dry-run first and --confirm only in a development database.");
        }

        var raw = await FindRawNoticeByUrlAsync(uri.ToString(), ct);
        if (raw == null)
        {
            if (request.PersistEvidence)
            {
                result.ImportJob = await _crawl.EnqueueManualUrlImportAsync(new ImportPublicUrlRequest
                {
                    DetailUrl = uri.ToString(),
                    NoticeType = "AwardAnnouncement"
                }, ct);
                result.Warnings.Add(
                    $"award raw notice not found; manual import job queued as {result.ImportJob.JobId}. Run the BidOps Worker, wait for attachment/text extraction, then call this debug endpoint again.");
            }
            else
            {
                result.Warnings.Add("award raw notice not found and persistEvidence=false, so no import job was queued.");
            }

            result.Warnings.Add("candidate notice not found");
            result.Warnings.Add("tender notice not found");
            result.Warnings.Add("award evidence not extracted because the raw award notice is not available yet.");
            return result;
        }

        return await ReverseCloseRawNoticeCoreAsync(raw, uri.ToString(), ct);
    }

    public async Task<BidOpsReverseClosureDebugResult> ReverseCloseRawNoticeAsync(
        long rawNoticeId,
        CancellationToken ct = default)
    {
        var raw = await _rawNotices.GetByIdAsync(rawNoticeId, ct);
        if (raw == null)
            throw new AtlasException($"BidOps raw notice does not exist: {rawNoticeId}");

        return await ReverseCloseRawNoticeCoreAsync(raw, raw.DetailUrl, ct);
    }

    public static IReadOnlyList<LifecyclePackageClosure> LinkEvidenceForDebug(
        IReadOnlyList<AwardEvidence> awards,
        IReadOnlyList<CandidateEvidence> candidates,
        IReadOnlyList<TenderPackageEvidence> tenders)
    {
        return BuildClosures(awards, candidates, tenders);
    }

    private async Task<BidOpsReverseClosureDebugResult> ReverseCloseRawNoticeCoreAsync(
        RawNotice awardRaw,
        string inputUrl,
        CancellationToken ct)
    {
        var result = new BidOpsReverseClosureDebugResult
        {
            InputAwardNoticeUrl = inputUrl,
            AwardNotice = new RawNoticeDebugRef(
                awardRaw.Id,
                awardRaw.Title,
                NormalizeAwardNoticeType(awardRaw.NoticeType, awardRaw.Title, awardRaw.DetailUrl),
                awardRaw.DetailUrl,
                awardRaw.PublishTime,
                awardRaw.FetchTime,
                awardRaw.Status.ToString())
        };

        var awardDocuments = await ReadEvidenceDocumentsAsync(awardRaw, ct);
        if (awardDocuments.Count == 0)
            result.Warnings.Add("award raw notice text unavailable");

        var awardEvidence = BidOpsAwardEvidenceParser.Extract(awardDocuments);
        result.AwardEvidence.AddRange(awardEvidence);
        if (awardEvidence.Count == 0)
        {
            result.Warnings.Add("parser template not matched for award evidence");
            result.Warnings.Add("award amount missing");
            return result;
        }

        var relatedRaw = await LoadRelatedRawNoticesAsync(awardRaw, ct);
        var candidateDocuments = new List<(RawNotice Raw, IReadOnlyList<BidOpsEvidenceDocument> Documents)>();
        var tenderDocuments = new List<(RawNotice Raw, IReadOnlyList<BidOpsEvidenceDocument> Documents)>();
        foreach (var raw in relatedRaw)
        {
            if (LooksLikeCandidateNotice(raw))
            {
                var documents = await ReadEvidenceDocumentsAsync(raw, ct);
                var match = MatchNotice(raw, documents, awardEvidence, "Candidate");
                if (match.Confidence > 0 || match.MissingReason == null)
                    result.CandidateNoticeMatches.Add(match);
                if (match.Confidence >= 0.45)
                    candidateDocuments.Add((raw, documents));
            }
            else if (LooksLikeTenderNotice(raw))
            {
                var documents = await ReadEvidenceDocumentsAsync(raw, ct);
                var match = MatchNotice(raw, documents, awardEvidence, "Tender");
                if (match.Confidence > 0 || match.MissingReason == null)
                    result.TenderNoticeMatches.Add(match);
                if (match.Confidence >= 0.4)
                    tenderDocuments.Add((raw, documents));
            }
        }

        var candidateEvidence = candidateDocuments
            .SelectMany(x => BidOpsCandidateEvidenceParser.Extract(x.Documents))
            .ToList();
        var tenderEvidence = tenderDocuments
            .SelectMany(x => BidOpsTenderPackageEvidenceParser.Extract(x.Documents))
            .ToList();

        result.CandidateEvidence.AddRange(candidateEvidence);
        result.TenderPackageEvidence.AddRange(tenderEvidence);
        if (candidateEvidence.Count == 0)
            result.Warnings.Add("candidate notice not found or parser template not matched");
        if (tenderEvidence.Count == 0)
            result.Warnings.Add("tender notice not found or parser template not matched");

        result.Closures.AddRange(BuildClosures(awardEvidence, candidateEvidence, tenderEvidence));
        if (result.Closures.Count == 0)
            result.Warnings.Add("no lifecycle package closure could be suggested");

        foreach (var closure in result.Closures)
        {
            foreach (var missing in closure.MissingFields)
                result.Warnings.Add($"{closure.PackageNo ?? "unknown package"}: {missing}");
        }

        result.Warnings = result.Warnings.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
        result.CandidateNoticeMatches = result.CandidateNoticeMatches
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.PublishTime)
            .Take(20)
            .ToList();
        result.TenderNoticeMatches = result.TenderNoticeMatches
            .OrderByDescending(x => x.Confidence)
            .ThenByDescending(x => x.PublishTime)
            .Take(20)
            .ToList();
        return result;
    }

    private async Task<RawNotice?> FindRawNoticeByUrlAsync(string url, CancellationToken ct)
    {
        var hash = _hasher.HashUrl(url);
        var builder = await _rawNotices.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.DetailUrlHash == hash || x.DetailUrl == url)
            .OrderByDescending(x => x.FetchTime)
            .FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyList<RawNotice>> LoadRelatedRawNoticesAsync(
        RawNotice awardRaw,
        CancellationToken ct)
    {
        var builder = await _rawNotices.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        return await builder
            .Where(x => x.Id != awardRaw.Id)
            .OrderByDescending(x => x.FetchTime)
            .Take(RelatedNoticeScanLimit)
            .ToListAsync(ct);
    }

    private async Task<IReadOnlyList<BidOpsEvidenceDocument>> ReadEvidenceDocumentsAsync(
        RawNotice raw,
        CancellationToken ct)
    {
        var documents = new List<BidOpsEvidenceDocument>();
        var rawText = await TryReadStoredTextAsync(raw.TextContentStorageKey, raw.TextPreview, raw.Id, ct);
        if (!string.IsNullOrWhiteSpace(rawText))
        {
            documents.Add(new BidOpsEvidenceDocument(
                new EvidenceSourceRef(
                    raw.Id,
                    null,
                    NormalizeAwardNoticeType(raw.NoticeType, raw.Title, raw.DetailUrl),
                    raw.DetailUrl,
                    null,
                    null,
                    null,
                    null,
                    null),
                raw.Title,
                raw.NoticeType,
                raw.PublishTime,
                rawText));
        }

        var attachmentBuilder = await _rawAttachments.QueryDataScopeAsync(BidOpsDataResources.RawNotice, AtlasDataScopeType.AllTenant, ct);
        var attachments = await attachmentBuilder
            .Where(x => x.RawNoticeId == raw.Id &&
                        x.TextExtractStatus == TextExtractStatus.Succeeded &&
                        x.TextContentStorageKey != string.Empty)
            .OrderBy(x => x.Id)
            .ToListAsync(ct);
        foreach (var attachment in attachments)
        {
            var text = await TryReadStoredTextAsync(attachment.TextContentStorageKey, string.Empty, raw.Id, ct);
            if (string.IsNullOrWhiteSpace(text))
                continue;

            documents.Add(new BidOpsEvidenceDocument(
                new EvidenceSourceRef(
                    raw.Id,
                    attachment.Id,
                    NormalizeAwardNoticeType(raw.NoticeType, raw.Title, raw.DetailUrl),
                    raw.DetailUrl,
                    attachment.FileName,
                    null,
                    null,
                    null,
                    null),
                raw.Title,
                raw.NoticeType,
                raw.PublishTime,
                text));
        }

        return documents;
    }

    private async Task<string> TryReadStoredTextAsync(
        string storageKey,
        string fallback,
        long rawNoticeId,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(storageKey))
            return fallback;

        try
        {
            await using var stream = await _fileStore.OpenReadAsync(storageKey, ct);
            using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
            var text = await reader.ReadToEndAsync(ct);
            return text.Length <= StoredTextReadLimit ? text : text[..StoredTextReadLimit];
        }
        catch (Exception ex) when (ex is IOException or FileNotFoundException or DirectoryNotFoundException or UnauthorizedAccessException)
        {
            _logger.LogWarning(
                ex,
                "BidOps reverse lifecycle closure could not read stored text {StorageKey} for raw notice {RawNoticeId}.",
                storageKey,
                rawNoticeId);
            return fallback;
        }
    }

    private static BidOpsNoticeMatch MatchNotice(
        RawNotice raw,
        IReadOnlyList<BidOpsEvidenceDocument> documents,
        IReadOnlyList<AwardEvidence> awards,
        string stage)
    {
        var text = string.Join('\n', documents.Select(x => x.Text));
        var reasons = new List<string>();
        var confidence = 0d;
        var projectCodes = awards.Select(x => x.ProjectCode).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var projectNames = awards.Select(x => x.ProjectName).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        var packageNos = awards.Select(x => x.NormalizedPackageNo).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();

        var noticeProjectCode = BidOpsEvidenceText.ExtractProjectCode(text);
        if (!string.IsNullOrWhiteSpace(noticeProjectCode) &&
            projectCodes.Any(x => string.Equals(x, noticeProjectCode, StringComparison.OrdinalIgnoreCase)))
        {
            confidence += 0.45;
            reasons.Add("ProjectCode exact match");
        }

        var noticeProjectName = FirstNonEmpty(BidOpsEvidenceText.ExtractProjectName(text), raw.Title);
        if (projectNames.Any(x => BidOpsEvidenceText.Similarity(x, noticeProjectName) >= 0.72))
        {
            confidence += 0.25;
            reasons.Add("ProjectName similarity match");
        }

        if (packageNos.Any(x => ContainsNormalizedPackageNo(text, x)))
        {
            confidence += 0.15;
            reasons.Add("PackageNo normalized hint matched");
        }

        if (stage == "Candidate" && LooksLikeCandidateNotice(raw))
        {
            confidence += 0.15;
            reasons.Add("Notice type/title indicates candidate announcement");
        }
        else if (stage == "Tender" && LooksLikeTenderNotice(raw))
        {
            confidence += 0.12;
            reasons.Add("Notice type/title indicates tender/procurement announcement");
        }

        var missingReason = confidence <= 0 ? "project code/name/package hint not matched" : null;
        return new BidOpsNoticeMatch(
            raw.Id,
            raw.Title,
            raw.NoticeType,
            raw.DetailUrl,
            raw.PublishTime,
            Math.Min(1d, confidence),
            reasons,
            missingReason);
    }

    private static IReadOnlyList<LifecyclePackageClosure> BuildClosures(
        IReadOnlyList<AwardEvidence> awards,
        IReadOnlyList<CandidateEvidence> candidates,
        IReadOnlyList<TenderPackageEvidence> tenders)
    {
        var closures = new List<LifecyclePackageClosure>();
        foreach (var award in awards)
        {
            var packageCandidates = candidates
                .Where(candidate => PackageContextMatches(award.LotNo, award.NormalizedPackageNo, candidate.LotNo, candidate.NormalizedPackageNo))
                .Where(candidate => ProjectMatches(award.ProjectCode, award.ProjectName, candidate.ProjectCode, candidate.ProjectName))
                .OrderBy(candidate => candidate.Rank ?? 99)
                .ThenByDescending(candidate => candidate.FinalQuoteAmount.HasValue)
                .ToList();
            var matchedCandidate = packageCandidates
                .Where(candidate => SupplierMatches(award.AwardedSupplierName, candidate.SupplierName))
                .OrderBy(candidate => candidate.Rank == 1 ? 0 : 1)
                .ThenBy(candidate => candidate.Rank ?? 99)
                .FirstOrDefault();
            var packageTenders = tenders
                .Where(tender => PackageContextMatches(award.LotNo, award.NormalizedPackageNo, tender.LotNo, tender.NormalizedPackageNo))
                .Where(tender => ProjectMatches(award.ProjectCode, award.ProjectName, tender.ProjectCode, tender.ProjectName))
                .ToList();
            var tender = MergeTenderEvidence(packageTenders, award, matchedCandidate);

            var reasons = new List<string>();
            var missing = new List<string>();
            var confidence = 0.35d;
            if (!string.IsNullOrWhiteSpace(award.ProjectCode))
            {
                confidence += 0.15;
                reasons.Add("ProjectCode present on award evidence");
            }
            else
            {
                missing.Add("project code missing");
            }

            if (!string.IsNullOrWhiteSpace(award.NormalizedPackageNo))
            {
                confidence += 0.15;
                reasons.Add("PackageNo normalized from award evidence");
            }
            else
            {
                missing.Add("package number ambiguous");
            }

            if (matchedCandidate != null)
            {
                confidence += 0.2;
                reasons.Add("Awarded supplier matched candidate");
                if (matchedCandidate.Rank == 1)
                {
                    confidence += 0.1;
                    reasons.Add("Awarded supplier matched candidate rank 1");
                }
            }
            else if (packageCandidates.Count > 0)
            {
                confidence += 0.08;
                missing.Add("awarded supplier did not match candidate supplier");
            }
            else
            {
                missing.Add("candidate notice not found");
            }

            if (tender != null)
            {
                confidence += 0.08;
                reasons.Add("Tender/procurement package evidence matched");
                if (tender.BudgetAmount == null && tender.MaxPrice == null)
                    missing.Add("tender amount missing");
            }
            else
            {
                missing.Add("tender notice not found");
            }

            decimal? finalAmount;
            string amountSource;
            EvidenceSourceRef? amountEvidence;
            if (award.AwardAmount.HasValue)
            {
                finalAmount = award.AwardAmount;
                amountSource = "AwardNoticeAmount";
                amountEvidence = award.Evidence;
                reasons.Add("Award amount taken from award notice");
            }
            else if (matchedCandidate?.FinalQuoteAmount.HasValue == true &&
                     (matchedCandidate.Rank == 1 ||
                      packageCandidates.Count(candidate => SupplierMatches(award.AwardedSupplierName, candidate.SupplierName)) == 1))
            {
                finalAmount = matchedCandidate.FinalQuoteAmount;
                amountSource = "CandidateFinalQuote";
                amountEvidence = matchedCandidate.Evidence;
                reasons.Add("Award amount taken from candidate final quote");
                confidence += 0.07;
            }
            else
            {
                finalAmount = null;
                amountSource = "Missing";
                amountEvidence = null;
                missing.Add("award amount missing");
            }

            closures.Add(new LifecyclePackageClosure(
                ProjectCode: FirstNonEmpty(award.ProjectCode, matchedCandidate?.ProjectCode, tender?.ProjectCode),
                ProjectName: FirstNonEmpty(award.ProjectName, matchedCandidate?.ProjectName, tender?.ProjectName),
                ProjectUnit: FirstNonEmpty(award.ProjectUnit),
                LotNo: FirstNonEmpty(award.LotNo, matchedCandidate?.LotNo, tender?.LotNo),
                LotName: FirstNonEmpty(award.LotName, tender?.LotName),
                PackageNo: FirstNonEmpty(award.PackageNo, matchedCandidate?.PackageNo, tender?.PackageNo),
                NormalizedPackageNo: FirstNonEmpty(award.NormalizedPackageNo, matchedCandidate?.NormalizedPackageNo, tender?.NormalizedPackageNo),
                PackageName: FirstNonEmpty(award.PackageName, matchedCandidate?.PackageName, tender?.PackageName),
                Tender: tender,
                Candidates: packageCandidates,
                Award: award,
                FinalAwardAmount: finalAmount,
                FinalAwardAmountSource: amountSource,
                AmountEvidence: amountEvidence,
                LinkConfidence: Math.Min(1d, Math.Round(confidence, 3)),
                MatchReasons: reasons.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                MissingFields: missing.Distinct(StringComparer.OrdinalIgnoreCase).ToList(),
                RequiresManualReview: confidence < 0.9d || missing.Count > 0));
        }

        return closures;
    }

    private static TenderPackageEvidence? MergeTenderEvidence(
        IReadOnlyList<TenderPackageEvidence> tenders,
        AwardEvidence award,
        CandidateEvidence? candidate)
    {
        if (tenders.Count == 0)
            return null;

        var ordered = tenders
            .OrderByDescending(x => !string.IsNullOrWhiteSpace(x.ScopeText))
            .ThenByDescending(x => x.BudgetAmount.HasValue || x.MaxPrice.HasValue)
            .ThenByDescending(x => !string.IsNullOrWhiteSpace(x.QualificationText))
            .ToList();
        var primary = ordered[0];
        return primary with
        {
            ProjectCode = FirstNonEmpty(primary.ProjectCode, candidate?.ProjectCode, award.ProjectCode),
            ProjectName = FirstNonEmpty(primary.ProjectName, candidate?.ProjectName, award.ProjectName),
            LotNo = FirstNonEmpty(primary.LotNo, candidate?.LotNo, award.LotNo),
            LotName = FirstNonEmpty(primary.LotName, award.LotName),
            PackageNo = FirstNonEmpty(primary.PackageNo, candidate?.PackageNo, award.PackageNo),
            NormalizedPackageNo = FirstNonEmpty(primary.NormalizedPackageNo, candidate?.NormalizedPackageNo, award.NormalizedPackageNo),
            PackageName = FirstNonEmpty(primary.PackageName, candidate?.PackageName, award.PackageName),
            ScopeText = FirstNonEmpty(Prepend(primary.ScopeText, ordered.Select(x => x.ScopeText))),
            BudgetAmount = primary.BudgetAmount ?? ordered.Select(x => x.BudgetAmount).FirstOrDefault(x => x.HasValue),
            MaxPrice = primary.MaxPrice ?? ordered.Select(x => x.MaxPrice).FirstOrDefault(x => x.HasValue),
            QualificationText = FirstNonEmpty(Prepend(primary.QualificationText, ordered.Select(x => x.QualificationText))),
            PerformanceRequirement = FirstNonEmpty(Prepend(primary.PerformanceRequirement, ordered.Select(x => x.PerformanceRequirement))),
            PersonnelRequirement = FirstNonEmpty(Prepend(primary.PersonnelRequirement, ordered.Select(x => x.PersonnelRequirement)))
        };
    }

    private static bool PackageMatches(string? left, string? right)
    {
        return !string.IsNullOrWhiteSpace(left) &&
               !string.IsNullOrWhiteSpace(right) &&
               string.Equals(left, right, StringComparison.OrdinalIgnoreCase);
    }

    private static bool PackageContextMatches(
        string? awardLotNo,
        string? awardPackageNo,
        string? otherLotNo,
        string? otherPackageNo)
    {
        if (!PackageMatches(awardPackageNo, otherPackageNo))
            return false;

        if (string.IsNullOrWhiteSpace(awardLotNo) || string.IsNullOrWhiteSpace(otherLotNo))
            return true;

        return string.Equals(
            BidOpsTextQuality.CleanExtractedValue(awardLotNo),
            BidOpsTextQuality.CleanExtractedValue(otherLotNo),
            StringComparison.OrdinalIgnoreCase);
    }

    private static bool ProjectMatches(
        string? awardProjectCode,
        string? awardProjectName,
        string? otherProjectCode,
        string? otherProjectName)
    {
        if (!string.IsNullOrWhiteSpace(awardProjectCode) &&
            !string.IsNullOrWhiteSpace(otherProjectCode))
        {
            return string.Equals(awardProjectCode, otherProjectCode, StringComparison.OrdinalIgnoreCase);
        }

        if (!string.IsNullOrWhiteSpace(awardProjectName) &&
            !string.IsNullOrWhiteSpace(otherProjectName))
        {
            return BidOpsEvidenceText.Similarity(awardProjectName, otherProjectName) >= 0.55;
        }

        return true;
    }

    private static bool SupplierMatches(string? left, string? right)
    {
        var normalizedLeft = BidOpsSupplierNameNormalizer.NormalizeForMatch(left);
        var normalizedRight = BidOpsSupplierNameNormalizer.NormalizeForMatch(right);
        return !string.IsNullOrWhiteSpace(normalizedLeft) &&
               !string.IsNullOrWhiteSpace(normalizedRight) &&
               string.Equals(normalizedLeft, normalizedRight, StringComparison.OrdinalIgnoreCase);
    }

    private static bool ContainsNormalizedPackageNo(string text, string? normalizedPackageNo)
    {
        if (string.IsNullOrWhiteSpace(normalizedPackageNo))
            return false;

        return text.Split(['\r', '\n', '。', '；', ';'], StringSplitOptions.RemoveEmptyEntries)
            .Select(BidOpsPackageNoNormalizer.Normalize)
            .Any(x => string.Equals(x, normalizedPackageNo, StringComparison.OrdinalIgnoreCase));
    }

    private static bool LooksLikeCandidateNotice(RawNotice raw)
    {
        return ContainsAny($"{raw.NoticeType} {raw.Title}", "Candidate", "候选人", "推荐成交候选", "推荐中标候选", "公示");
    }

    private static bool LooksLikeTenderNotice(RawNotice raw)
    {
        var signal = $"{raw.NoticeType} {raw.Title}";
        if (ContainsAny(signal, "Candidate", "Award", "中标", "成交结果", "候选"))
            return false;

        return ContainsAny(signal, "Tender", "Procurement", "招标公告", "采购公告", "公开谈判采购", "竞争性谈判采购", "询价采购");
    }

    private static string NormalizeAwardNoticeType(string noticeType, string title, string detailUrl)
    {
        var signal = $"{noticeType} {title} {detailUrl}";
        if (ContainsAny(signal, "doci-win", "Award", "Win", "中标结果", "成交结果", "中标公告", "成交公告"))
            return "AwardNotice";
        if (ContainsAny(signal, "Candidate", "候选"))
            return "CandidateNotice";
        if (ContainsAny(signal, "Tender", "Procurement", "招标公告", "采购公告"))
            return "TenderAnnouncement";
        return noticeType;
    }

    private static bool ContainsAny(string value, params string[] tokens)
    {
        return tokens.Any(token => value.Contains(token, StringComparison.OrdinalIgnoreCase));
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        foreach (var value in values)
        {
            var cleaned = BidOpsTextQuality.CleanExtractedValue(value);
            if (!string.IsNullOrWhiteSpace(cleaned))
                return cleaned;
        }

        return null;
    }

    private static string?[] Prepend(string? value, IEnumerable<string?> values)
    {
        return new[] { value }.Concat(values).ToArray();
    }
}
