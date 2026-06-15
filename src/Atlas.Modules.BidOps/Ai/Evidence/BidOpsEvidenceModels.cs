using Atlas.Modules.BidOps.Models;

namespace Atlas.Modules.BidOps.Ai.Evidence;

public sealed record EvidenceSourceRef(
    long? RawNoticeId,
    long? RawAttachmentId,
    string? NoticeType,
    string? SourceUrl,
    string? AttachmentName,
    int? TableIndex,
    int? RowIndex,
    int? ColumnIndex,
    string? EvidenceText);

public sealed record AwardEvidence(
    string? ProjectCode,
    string? ProjectName,
    string? ProjectUnit,
    string? LotNo,
    string? LotName,
    string? PackageNo,
    string? NormalizedPackageNo,
    string? PackageName,
    string AwardedSupplierName,
    decimal? AwardAmount,
    string AmountSource,
    EvidenceSourceRef Evidence,
    double Confidence);

public sealed record CandidateEvidence(
    string? ProjectCode,
    string? ProjectName,
    string? LotNo,
    string? PackageNo,
    string? NormalizedPackageNo,
    string? PackageName,
    string SupplierName,
    int? Rank,
    decimal? FinalQuoteAmount,
    string? Quality,
    string? Duration,
    string? QualificationStatus,
    string? EvaluationText,
    EvidenceSourceRef Evidence,
    double Confidence);

public sealed record TenderPackageEvidence(
    string? ProjectCode,
    string? ProjectName,
    string? LotNo,
    string? LotName,
    string? PackageNo,
    string? NormalizedPackageNo,
    string? PackageName,
    string? Category,
    string? ScopeText,
    decimal? BudgetAmount,
    decimal? MaxPrice,
    string? Quantity,
    string? DeliveryPlace,
    string? DeliveryPeriod,
    string? QualificationText,
    string? PerformanceRequirement,
    string? PersonnelRequirement,
    EvidenceSourceRef Evidence,
    double Confidence);

public sealed record LifecyclePackageClosure(
    string? ProjectCode,
    string? ProjectName,
    string? ProjectUnit,
    string? LotNo,
    string? LotName,
    string? PackageNo,
    string? NormalizedPackageNo,
    string? PackageName,
    TenderPackageEvidence? Tender,
    IReadOnlyList<CandidateEvidence> Candidates,
    AwardEvidence Award,
    decimal? FinalAwardAmount,
    string FinalAwardAmountSource,
    EvidenceSourceRef? AmountEvidence,
    double LinkConfidence,
    IReadOnlyList<string> MatchReasons,
    IReadOnlyList<string> MissingFields,
    bool RequiresManualReview);

public sealed record LifecycleLinkSuggestion(
    AwardEvidence Award,
    CandidateEvidence? MatchedCandidate,
    TenderPackageEvidence? MatchedTender,
    double Confidence,
    IReadOnlyList<string> MatchReasons,
    IReadOnlyList<string> MissingFields);

public sealed record BidOpsEvidenceDocument(
    EvidenceSourceRef Source,
    string Title,
    string NoticeType,
    DateTime? PublishTime,
    string Text);

public sealed record BidOpsNoticeMatch(
    long RawNoticeId,
    string Title,
    string NoticeType,
    string SourceUrl,
    DateTime? PublishTime,
    double Confidence,
    IReadOnlyList<string> MatchReasons,
    string? MissingReason);

public sealed class BidOpsReverseCloseUrlRequest
{
    public string Url { get; set; } = string.Empty;

    public bool ResetDerivedData { get; set; }

    public bool PersistEvidence { get; set; } = true;
}

public sealed class BidOpsReverseClosureDebugResult
{
    public string InputAwardNoticeUrl { get; set; } = string.Empty;

    public RawNoticeDebugRef? AwardNotice { get; set; }

    public EnqueueJobDto? ImportJob { get; set; }

    public List<AwardEvidence> AwardEvidence { get; set; } = [];

    public List<BidOpsNoticeMatch> CandidateNoticeMatches { get; set; } = [];

    public List<CandidateEvidence> CandidateEvidence { get; set; } = [];

    public List<BidOpsNoticeMatch> TenderNoticeMatches { get; set; } = [];

    public List<TenderPackageEvidence> TenderPackageEvidence { get; set; } = [];

    public List<LifecyclePackageClosure> Closures { get; set; } = [];

    public List<string> Warnings { get; set; } = [];
}

public sealed record RawNoticeDebugRef(
    long RawNoticeId,
    string Title,
    string NoticeType,
    string SourceUrl,
    DateTime? PublishTime,
    DateTime FetchTime,
    string Status);
