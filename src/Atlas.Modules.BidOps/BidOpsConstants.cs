namespace Atlas.Modules.BidOps;

public static class BidOpsCapabilities
{
    public const string Crawl = "bidops.crawl";
    public const string Review = "bidops.review";
    public const string Business = "bidops.business";
}

public static class BidOpsPermissionCodes
{
    public const string CrawlRead = "bidops.crawl.read";
    public const string CrawlManage = "bidops.crawl.manage";
    public const string CrawlImport = "bidops.crawl.import";
    public const string ReviewRead = "bidops.review.read";
    public const string ReviewApprove = "bidops.review.approve";
    public const string BusinessRead = "bidops.business.read";
}

public static class BidOpsDataResources
{
    public const string CrawlSource = "bidops.crawl-source";
    public const string RawNotice = "bidops.raw-notice";
    public const string ReviewTask = "bidops.review-task";
    public const string Notice = "bidops.notice";
    public const string TenderPackage = "bidops.tender-package";
}

public static class BidOpsBackgroundJobQueues
{
    public const string BidOps = "bidops";
}

public static class BidOpsBackgroundJobTypes
{
    public const string ManualUrlImport = "bidops.raw.manual-url-import";
    public const string MockCrawl = "bidops.crawl.mock-scan";
    public const string StateGridEcpCrawl = "bidops.crawl.state-grid-ecp-scan";
    public const string AttachmentProcess = "bidops.document.attachment-process";
    public const string StructuredParse = "bidops.ai.structured-parse";
    public const string MockAiParse = "bidops.ai.mock-parse";
}

public static class BidOpsCrawlSourceTypes
{
    public const string Mock = "Mock";
    public const string Manual = "Manual";
    public const string StateGridEcp = "StateGridEcp";
}

public static class BidOpsSystemValues
{
    public const string ModuleName = "BidOps";
    public const string ManualSourceCode = "manual";
    public const string MockSourceCode = "mock-public";
    public const string StateGridEcpSourceCode = "state-grid-ecp";
    public const string LocalStorageProvider = "Local";
    public const string StructuredParserVersion = "v2";
}
