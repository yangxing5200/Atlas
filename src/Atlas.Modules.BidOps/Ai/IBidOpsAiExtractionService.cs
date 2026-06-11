namespace Atlas.Modules.BidOps.Ai;

public sealed record BidOpsNoticeExtract(
    string NoticeType,
    string ProjectName,
    string ProjectCode,
    string BuyerName,
    string AgencyName,
    string Region,
    decimal? BudgetAmount,
    DateTime? PublishTime,
    DateTime? SignupDeadline,
    DateTime? BidDeadline,
    DateTime? OpenBidTime,
    decimal Confidence,
    IReadOnlyList<BidOpsPackageExtract> Packages);

public sealed record BidOpsPackageExtract(
    string LotNo,
    string LotName,
    string PackageNo,
    string PackageName,
    string Category,
    decimal? Quantity,
    string Unit,
    decimal? BudgetAmount,
    decimal? MaxPrice,
    string DeliveryPlace,
    string DeliveryPeriod,
    decimal Confidence,
    IReadOnlyList<BidOpsRequirementExtract> Requirements);

public sealed record BidOpsRequirementExtract(
    string RequirementType,
    string OriginalText,
    int? SourcePage,
    bool IsMandatory,
    bool IsRejectRisk,
    string RequiredEvidenceType,
    string RiskLevel,
    string AiExplanation,
    decimal Confidence);

public interface IBidOpsAiExtractionService
{
    Task<BidOpsNoticeExtract> ExtractAsync(
        string title,
        string text,
        CancellationToken cancellationToken = default);
}
