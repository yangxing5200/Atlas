namespace Atlas.Modules.BidOps.Ai;

public interface IBidOpsLifecycleFieldEnrichmentAiService
{
    Task<BidOpsLifecycleFieldEnrichmentResult> EnrichAsync(
        BidOpsLifecycleFieldEnrichmentRequest request,
        CancellationToken ct = default);
}

public sealed record BidOpsLifecycleFieldEnrichmentRequest(
    long LinkId,
    string ProjectCode,
    string ProjectName,
    string LotNo,
    string LotName,
    string PackageNo,
    string PackageName,
    string SupplierName,
    decimal? FinalAwardAmount,
    string FinalAwardAmountSource,
    string EvidenceJson,
    IReadOnlyList<BidOpsLifecycleFieldEvidenceInput> Evidence,
    string? ReviewerPrompt = null);

public sealed record BidOpsLifecycleFieldEvidenceInput(
    string Stage,
    long? RawNoticeId,
    long? RawAttachmentId,
    string Title,
    string NoticeType,
    string SourceUrl,
    string AttachmentName,
    string Text);

public sealed record BidOpsLifecycleFieldEnrichmentResult(
    IReadOnlyList<BidOpsLifecycleFieldSuggestion> Fields,
    decimal Confidence,
    bool RequiresManualReview,
    string Summary,
    IReadOnlyList<string> Conflicts);

public sealed record BidOpsLifecycleFieldSuggestion(
    string FieldName,
    string Value,
    decimal? NumericValue,
    string SourceStage,
    long? SourceRawNoticeId,
    long? SourceRawAttachmentId,
    string EvidenceText,
    decimal Confidence,
    string Reason);
