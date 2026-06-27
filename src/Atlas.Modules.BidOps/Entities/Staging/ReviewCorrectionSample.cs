namespace Atlas.Modules.BidOps.Entities.Staging;

public sealed class ReviewCorrectionSample : BidOpsTenantEntity
{
    public long ReviewTaskId { get; set; }

    public long RawNoticeId { get; set; }

    public string NoticeType { get; set; } = string.Empty;

    public string SourceKind { get; set; } = BidOpsReviewCorrectionSourceKinds.ManualEdit;

    public string FieldName { get; set; } = string.Empty;

    public string OriginalValue { get; set; } = string.Empty;

    public string CorrectedValue { get; set; } = string.Empty;

    public string OriginalHeader { get; set; } = string.Empty;

    public string OriginalRowJson { get; set; } = string.Empty;

    public string ReviewerPrompt { get; set; } = string.Empty;

    public string Reason { get; set; } = string.Empty;

    public long? CreatedBy { get; set; }
}

public static class BidOpsReviewCorrectionSourceKinds
{
    public const string ManualEdit = "ManualEdit";
    public const string BulkApprove = "BulkApprove";
    public const string ReparsePrompt = "ReparsePrompt";
    public const string ApprovalOutcomeExtract = "ApprovalOutcomeExtract";
    public const string IssueResolved = "IssueResolved";
}
