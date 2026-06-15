namespace Atlas.Modules.BidOps.Ai;

public interface IBidOpsOutcomeSupplierAiExtractionService
{
    Task<IReadOnlyList<BidOpsOutcomeSupplierExtract>> ExtractAsync(
        BidOpsOutcomeSupplierAiExtractionRequest request,
        CancellationToken ct = default);
}

public sealed record BidOpsOutcomeSupplierAiExtractionRequest(
    string Title,
    string NoticeType,
    string SourceUrl,
    DateTime? PublishTime,
    string Text,
    IReadOnlyList<BidOpsOutcomeSupplierExtract> DeterministicExtracts,
    string? ReviewerPrompt = null,
    string Html = "",
    IReadOnlyList<BidOpsAiAttachmentInput>? Attachments = null);
