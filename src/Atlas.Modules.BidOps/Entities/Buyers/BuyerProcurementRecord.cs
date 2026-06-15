using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Buyers;

public sealed class BuyerProcurementRecord : BidOpsTenantEntity
{
    public long BuyerId { get; set; }

    public long RawNoticeId { get; set; }

    public long? NoticeId { get; set; }

    public string SourceUrl { get; set; } = string.Empty;

    public string NoticeTitle { get; set; } = string.Empty;

    public string NoticeType { get; set; } = string.Empty;

    public string ProjectName { get; set; } = string.Empty;

    public string ProjectCode { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public DateTime? PublishTime { get; set; }

    public decimal? BudgetAmount { get; set; }

    public int PackageCount { get; set; }

    public string SourceHash { get; set; } = string.Empty;

    public string Remark { get; set; } = string.Empty;
}
