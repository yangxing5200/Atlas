namespace Atlas.Modules.BidOps.Entities;

public enum RawNoticeStatus
{
    New = 0,
    ParseQueued = 1,
    ReviewPending = 2,
    Approved = 3,
    Ignored = 4,
    Failed = 5
}

public enum ReviewStatus
{
    Pending = 0,
    InReview = 1,
    Approved = 2,
    Ignored = 3,
    ReparseRequired = 4
}

public enum ReviewTaskStatus
{
    Pending = 0,
    InReview = 1,
    Approved = 2,
    Ignored = 3,
    Merged = 4,
    ReparseRequired = 5
}

public enum ReviewQualityRiskLevel
{
    Low = 0,
    Medium = 1,
    High = 2
}

public enum ReviewRecommendation
{
    BatchConfirmCandidate = 0,
    NeedsReview = 1,
    NeedsReparse = 2
}

public enum DownloadStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3
}

public enum TextExtractStatus
{
    Pending = 0,
    Succeeded = 1,
    Failed = 2,
    Skipped = 3
}
