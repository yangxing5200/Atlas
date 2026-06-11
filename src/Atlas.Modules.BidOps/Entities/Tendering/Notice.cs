using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Tendering;

public sealed class Notice : BidOpsTenantEntity
{
    public long RawNoticeId { get; set; }

    public long NoticeStagingId { get; set; }

    public string Title { get; set; } = string.Empty;

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

    public string Status { get; set; } = "Active";
}
