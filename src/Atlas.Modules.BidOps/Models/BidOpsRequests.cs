using Atlas.Modules.BidOps.Entities;

namespace Atlas.Modules.BidOps.Models;

public class BidOpsPagedQuery
{
    public string? Keyword { get; set; }
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
}

public sealed class RawNoticeSearchQuery : BidOpsPagedQuery
{
    public RawNoticeStatus? Status { get; set; }
}

public sealed class ReviewTaskSearchQuery : BidOpsPagedQuery
{
    public ReviewTaskStatus? Status { get; set; }
}

public sealed class PackageSearchQuery : BidOpsPagedQuery
{
    public long? NoticeId { get; set; }
}

public class CreateCrawlSourceRequest
{
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SourceType { get; set; } = "Mock";
    public string BaseUrl { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
    public int RateLimitPerMinute { get; set; } = 10;
    public int CrawlIntervalMinutes { get; set; } = 60;
    public int MaxRetryCount { get; set; } = 3;
    public bool NeedJsRender { get; set; }
    public bool NeedLogin { get; set; }
    public bool RespectRobots { get; set; } = true;
    public string RobotsPolicyNote { get; set; } = string.Empty;
    public string Remark { get; set; } = string.Empty;
}

public sealed class UpdateCrawlSourceRequest : CreateCrawlSourceRequest
{
}

public class CreateCrawlChannelRequest
{
    public long SourceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string NoticeType { get; set; } = "TenderAnnouncement";
    public string ListUrl { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Industry { get; set; } = string.Empty;
    public bool Enabled { get; set; } = true;
}

public sealed class UpdateCrawlChannelRequest : CreateCrawlChannelRequest
{
}

public sealed class ImportPublicUrlRequest
{
    public long? SourceId { get; set; }
    public long? ChannelId { get; set; }
    public string DetailUrl { get; set; } = string.Empty;
    public string? Title { get; set; }
    public string? NoticeType { get; set; }
    public string? TextContent { get; set; }
}

public sealed class ReviewDecisionRequest
{
    public string? Remark { get; set; }
}
