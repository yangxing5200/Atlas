using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Entities.Staging;

public sealed class ReviewTask : BidOpsTenantEntity
{
    public string BizType { get; set; } = string.Empty;

    public long BizId { get; set; }

    public long? RawNoticeId { get; set; }

    public string TaskTitle { get; set; } = string.Empty;

    public int Priority { get; set; }

    public ReviewTaskStatus Status { get; set; } = ReviewTaskStatus.Pending;

    public long? AssignedTo { get; set; }

    public long? ReviewerId { get; set; }

    public string Decision { get; set; } = string.Empty;

    public string Remark { get; set; } = string.Empty;

    public DateTime? ReviewedAt { get; set; }
}
