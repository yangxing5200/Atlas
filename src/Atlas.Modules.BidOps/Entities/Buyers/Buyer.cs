using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Buyers;

public sealed class Buyer : BidOpsTenantEntity
{
    public string BuyerNo { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public string NameNormalized { get; set; } = string.Empty;

    public string UnifiedSocialCreditCode { get; set; } = string.Empty;

    public string Region { get; set; } = string.Empty;

    public string SourceUrl { get; set; } = string.Empty;

    public string LastProjectCode { get; set; } = string.Empty;

    public string LastProjectName { get; set; } = string.Empty;

    public string LastNoticeTitle { get; set; } = string.Empty;

    public DateTime? LastSeenAtUtc { get; set; }

    public string Status { get; set; } = BidOpsBuyerStatuses.Active;

    public string Remark { get; set; } = string.Empty;
}

public static class BidOpsBuyerStatuses
{
    public const string Active = "Active";
    public const string Inactive = "Inactive";
}
