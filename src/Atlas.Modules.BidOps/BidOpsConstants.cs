namespace Atlas.Modules.BidOps;

public static class BidOpsCapabilities
{
    public const string Dashboard = "bidops.dashboard";
    public const string Crawl = "bidops.crawl";
    public const string Review = "bidops.review";
    public const string Business = "bidops.business";
    public const string Opportunity = "bidops.opportunity";
    public const string Supplier = "bidops.supplier";
    public const string Matching = "bidops.matching";
    public const string Pursuit = "bidops.pursuit";
    public const string Operations = "bidops.ops";
}

public static class BidOpsPermissionCodes
{
    public const string DashboardRead = "bidops.dashboard.read";
    public const string CrawlRead = "bidops.crawl.read";
    public const string CrawlManage = "bidops.crawl.manage";
    public const string CrawlImport = "bidops.crawl.import";
    public const string ReviewRead = "bidops.review.read";
    public const string ReviewApprove = "bidops.review.approve";
    public const string BusinessRead = "bidops.business.read";
    public const string OpportunityRead = "bidops.opportunity.read";
    public const string OpportunityManage = "bidops.opportunity.manage";
    public const string OpportunityWatch = "bidops.opportunity.watch";
    public const string OpportunityAssess = "bidops.opportunity.assess";
    public const string SupplierRead = "bidops.supplier.read";
    public const string SupplierManage = "bidops.supplier.manage";
    public const string SupplierEvidenceRead = "bidops.supplier.evidence.read";
    public const string SupplierEvidenceManage = "bidops.supplier.evidence.manage";
    public const string MatchingRead = "bidops.matching.read";
    public const string MatchingRun = "bidops.matching.run";
    public const string MatchingDecide = "bidops.matching.decide";
    public const string PursuitRead = "bidops.pursuit.read";
    public const string PursuitManage = "bidops.pursuit.manage";
    public const string PursuitTaskManage = "bidops.pursuit.task.manage";
    public const string PursuitFollowRecordManage = "bidops.pursuit.follow-record.manage";
    public const string OpsRead = "bidops.ops.read";
    public const string OpsManage = "bidops.ops.manage";
}

public static class BidOpsDataResources
{
    public const string Dashboard = "bidops.dashboard";
    public const string CrawlSource = "bidops.crawl-source";
    public const string CrawlRunLog = "bidops.crawl-run-log";
    public const string RawNotice = "bidops.raw-notice";
    public const string ReviewTask = "bidops.review-task";
    public const string Notice = "bidops.notice";
    public const string TenderPackage = "bidops.tender-package";
    public const string Opportunity = "bidops.opportunity";
    public const string Supplier = "bidops.supplier";
    public const string SupplierEvidence = "bidops.supplier-evidence";
    public const string OutcomeSupplierRecord = "bidops.outcome-supplier-record";
    public const string Matching = "bidops.matching";
    public const string GoNoGoDecision = "bidops.go-no-go-decision";
    public const string Pursuit = "bidops.pursuit";
    public const string PursuitTask = "bidops.pursuit-task";
    public const string Operation = "bidops.operation";
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
    public const string OpportunityValueAssessment = "bidops.opportunity.value-assessment";
    public const string OpportunityDeadlineReminder = "bidops.opportunity.deadline-reminder";
    public const string OpportunityWatchReminder = "bidops.opportunity.watch-reminder";
    public const string OpportunityStaleStateScan = "bidops.opportunity.stale-state-scan";
    public const string SupplierEvidenceExpiryScan = "bidops.supplier.evidence-expiry-scan";
    public const string SupplierMatchRun = "bidops.matching.supplier-match-run";
    public const string OutcomeSupplierExtract = "bidops.outcome.supplier-extract";
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
