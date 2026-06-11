using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

public sealed class NoticeStaging : BidOpsTenantEntity
{
    public long RawNoticeId { get; set; }

    public string NoticeType { get; set; } = "TenderAnnouncement";

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string BuyerName { get; set; } = string.Empty;

    public string AgencyName { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public decimal? BudgetAmount { get; set; }

    public DateTime? PublishTime { get; set; }

    public DateTime? SignupDeadline { get; set; }

    public DateTime? BidDeadline { get; set; }

    public DateTime? OpenBidTime { get; set; }

    public decimal AiConfidence { get; set; }

    public ReviewStatus ReviewStatus { get; set; } = ReviewStatus.Pending;

    public long? ReviewerId { get; set; }

    public DateTime? ReviewedAt { get; set; }

    public string RawAiOutputStorageKey { get; set; } = string.Empty;
}
