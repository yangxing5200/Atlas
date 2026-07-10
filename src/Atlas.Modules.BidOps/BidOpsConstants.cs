namespace Atlas.Modules.BidOps;

/// <summary>
/// BidOps 功能域编码，用于菜单、权限和功能分组。
/// </summary>
public static class BidOpsCapabilities
{
    /// <summary>
    /// BidOps 仪表盘功能域。
    /// </summary>
    public const string Dashboard = "bidops.dashboard";
    /// <summary>
    /// 采集中心功能域。
    /// </summary>
    public const string Crawl = "bidops.crawl";
    /// <summary>
    /// 审核中心功能域。
    /// </summary>
    public const string Review = "bidops.review";
    /// <summary>
    /// 业务库功能域。
    /// </summary>
    public const string Business = "bidops.business";
    /// <summary>
    /// 商机功能域。
    /// </summary>
    public const string Opportunity = "bidops.opportunity";
    /// <summary>
    /// 供应商功能域。
    /// </summary>
    public const string Supplier = "bidops.supplier";
    /// <summary>
    /// 供应商匹配功能域。
    /// </summary>
    public const string Matching = "bidops.matching";
    /// <summary>
    /// 投标作业功能域。
    /// </summary>
    public const string Pursuit = "bidops.pursuit";
    /// <summary>
    /// 运维配置功能域。
    /// </summary>
    public const string Operations = "bidops.ops";
}

/// <summary>
/// BidOps 权限编码，用于授权校验和菜单按钮控制。
/// </summary>
public static class BidOpsPermissionCodes
{
    /// <summary>
    /// 查看 BidOps 仪表盘权限。
    /// </summary>
    public const string DashboardRead = "bidops.dashboard.read";
    /// <summary>
    /// 查看采集数据权限。
    /// </summary>
    public const string CrawlRead = "bidops.crawl.read";
    /// <summary>
    /// 管理采集配置权限。
    /// </summary>
    public const string CrawlManage = "bidops.crawl.manage";
    /// <summary>
    /// 手工导入公告权限。
    /// </summary>
    public const string CrawlImport = "bidops.crawl.import";
    /// <summary>
    /// 查看审核任务权限。
    /// </summary>
    public const string ReviewRead = "bidops.review.read";
    /// <summary>
    /// 执行审核确认权限。
    /// </summary>
    public const string ReviewApprove = "bidops.review.approve";
    /// <summary>
    /// 查看正式业务库权限。
    /// </summary>
    public const string BusinessRead = "bidops.business.read";
    /// <summary>
    /// 查看商机权限。
    /// </summary>
    public const string OpportunityRead = "bidops.opportunity.read";
    /// <summary>
    /// 管理商机权限。
    /// </summary>
    public const string OpportunityManage = "bidops.opportunity.manage";
    /// <summary>
    /// 关注商机权限。
    /// </summary>
    public const string OpportunityWatch = "bidops.opportunity.watch";
    /// <summary>
    /// 评估商机权限。
    /// </summary>
    public const string OpportunityAssess = "bidops.opportunity.assess";
    /// <summary>
    /// 查看供应商权限。
    /// </summary>
    public const string SupplierRead = "bidops.supplier.read";
    /// <summary>
    /// 管理供应商权限。
    /// </summary>
    public const string SupplierManage = "bidops.supplier.manage";
    /// <summary>
    /// 查看供应商证明材料权限。
    /// </summary>
    public const string SupplierEvidenceRead = "bidops.supplier.evidence.read";
    /// <summary>
    /// 管理供应商证明材料权限。
    /// </summary>
    public const string SupplierEvidenceManage = "bidops.supplier.evidence.manage";
    /// <summary>
    /// 查看匹配结果权限。
    /// </summary>
    public const string MatchingRead = "bidops.matching.read";
    /// <summary>
    /// 发起供应商匹配权限。
    /// </summary>
    public const string MatchingRun = "bidops.matching.run";
    /// <summary>
    /// 做出 Go/No-Go 决策权限。
    /// </summary>
    public const string MatchingDecide = "bidops.matching.decide";
    /// <summary>
    /// 查看投标作业权限。
    /// </summary>
    public const string PursuitRead = "bidops.pursuit.read";
    /// <summary>
    /// 管理投标作业权限。
    /// </summary>
    public const string PursuitManage = "bidops.pursuit.manage";
    /// <summary>
    /// 管理投标作业任务权限。
    /// </summary>
    public const string PursuitTaskManage = "bidops.pursuit.task.manage";
    /// <summary>
    /// 管理投标跟进记录权限。
    /// </summary>
    public const string PursuitFollowRecordManage = "bidops.pursuit.follow-record.manage";
    /// <summary>
    /// 查看 BidOps 运维配置权限。
    /// </summary>
    public const string OpsRead = "bidops.ops.read";
    /// <summary>
    /// 管理 BidOps 运维配置权限。
    /// </summary>
    public const string OpsManage = "bidops.ops.manage";
}

/// <summary>
/// BidOps 数据资源编码，用于 Atlas 数据范围和授权目录。
/// </summary>
public static class BidOpsDataResources
{
    /// <summary>
    /// BidOps 仪表盘功能域。
    /// </summary>
    public const string Dashboard = "bidops.dashboard";
    /// <summary>
    /// 采集来源数据资源。
    /// </summary>
    public const string CrawlSource = "bidops.crawl-source";
    /// <summary>
    /// 采集运行日志数据资源。
    /// </summary>
    public const string CrawlRunLog = "bidops.crawl-run-log";
    /// <summary>
    /// 采集断点数据资源。
    /// </summary>
    public const string CrawlCheckpoint = "bidops.crawl-checkpoint";
    /// <summary>
    /// 原始公告数据资源。
    /// </summary>
    public const string RawNotice = "bidops.raw-notice";
    /// <summary>
    /// 审核任务数据资源。
    /// </summary>
    public const string ReviewTask = "bidops.review-task";
    /// <summary>
    /// 正式公告数据资源。
    /// </summary>
    public const string Notice = "bidops.notice";
    /// <summary>
    /// 正式包件数据资源。
    /// </summary>
    public const string TenderPackage = "bidops.tender-package";
    /// <summary>
    /// 商机功能域。
    /// </summary>
    public const string Opportunity = "bidops.opportunity";
    /// <summary>
    /// 采购人数据资源。
    /// </summary>
    public const string Buyer = "bidops.buyer";
    /// <summary>
    /// 采购人采购记录数据资源。
    /// </summary>
    public const string BuyerProcurement = "bidops.buyer-procurement";
    /// <summary>
    /// 供应商功能域。
    /// </summary>
    public const string Supplier = "bidops.supplier";
    /// <summary>
    /// 供应商证明材料数据资源。
    /// </summary>
    public const string SupplierEvidence = "bidops.supplier-evidence";
    /// <summary>
    /// 结果供应商记录数据资源。
    /// </summary>
    public const string OutcomeSupplierRecord = "bidops.outcome-supplier-record";
    /// <summary>
    /// 金额候选数据资源。
    /// </summary>
    public const string AmountCandidate = "bidops.amount-candidate";
    /// <summary>
    /// 闭环包件链接数据资源。
    /// </summary>
    public const string LifecyclePackageLink = "bidops.lifecycle-package-link";
    /// <summary>
    /// 供应商匹配功能域。
    /// </summary>
    public const string Matching = "bidops.matching";
    /// <summary>
    /// Go/No-Go 决策数据资源。
    /// </summary>
    public const string GoNoGoDecision = "bidops.go-no-go-decision";
    /// <summary>
    /// 投标作业功能域。
    /// </summary>
    public const string Pursuit = "bidops.pursuit";
    /// <summary>
    /// 投标作业任务数据资源。
    /// </summary>
    public const string PursuitTask = "bidops.pursuit-task";
    /// <summary>
    /// BidOps 操作数据资源。
    /// </summary>
    public const string Operation = "bidops.operation";
    /// <summary>
    /// 运行配置数据资源。
    /// </summary>
    public const string RuntimeSetting = "bidops.runtime-setting";
}

/// <summary>
/// 采集模式枚举值。
/// </summary>
public static class BidOpsCrawlModes
{
    /// <summary>
    /// 增量采集模式。
    /// </summary>
    public const string Incremental = "Incremental";
    /// <summary>
    /// 历史回填采集模式。
    /// </summary>
    public const string Backfill = "Backfill";
}

/// <summary>
/// 采集游标类型枚举值。
/// </summary>
public static class BidOpsCrawlCursorKinds
{
    /// <summary>
    /// 按页码推进采集游标。
    /// </summary>
    public const string PageIndex = "PageIndex";
}

/// <summary>
/// 采集断点状态枚举值。
/// </summary>
public static class BidOpsCrawlCheckpointStatuses
{
    /// <summary>
    /// 空闲状态。
    /// </summary>
    public const string Idle = "Idle";
    /// <summary>
    /// 运行中。
    /// </summary>
    public const string Running = "Running";
    /// <summary>
    /// 已暂停。
    /// </summary>
    public const string Paused = "Paused";
    /// <summary>
    /// 已完成。
    /// </summary>
    public const string Completed = "Completed";
    /// <summary>
    /// 失败。
    /// </summary>
    public const string Failed = "Failed";
}

/// <summary>
/// 采集运行状态枚举值。
/// </summary>
public static class BidOpsCrawlRunStatuses
{
    /// <summary>
    /// 运行中。
    /// </summary>
    public const string Running = "Running";
    /// <summary>
    /// 成功。
    /// </summary>
    public const string Succeeded = "Succeeded";
    /// <summary>
    /// 失败。
    /// </summary>
    public const string Failed = "Failed";
    /// <summary>
    /// 已取消。
    /// </summary>
    public const string Canceled = "Canceled";
}

/// <summary>
/// 采集计划模式枚举值。
/// </summary>
public static class BidOpsCrawlScheduleModes
{
    /// <summary>
    /// 按固定间隔扫描。
    /// </summary>
    public const string Interval = "Interval";
    /// <summary>
    /// 按每日固定时间扫描。
    /// </summary>
    public const string Daily = "Daily";
}

/// <summary>
/// 原始公告入库结果枚举值。
/// </summary>
public static class BidOpsRawIngestionStatuses
{
    /// <summary>
    /// 本次入库新建了记录。
    /// </summary>
    public const string Created = "Created";
    /// <summary>
    /// 本次入库发现内容变化。
    /// </summary>
    public const string Changed = "Changed";
    /// <summary>
    /// 本次入库刷新了已有记录。
    /// </summary>
    public const string Refreshed = "Refreshed";
    /// <summary>
    /// 本次入库更新了来源身份信息。
    /// </summary>
    public const string IdentityUpdated = "IdentityUpdated";
    /// <summary>
    /// 本次入库跳过处理。
    /// </summary>
    public const string Skipped = "Skipped";
}

/// <summary>
/// BidOps 后台任务队列编码。
/// </summary>
public static class BidOpsBackgroundJobQueues
{
    /// <summary>
    /// BidOps 后台任务队列。
    /// </summary>
    public const string BidOps = "bidops";
}

/// <summary>
/// BidOps 后台任务类型编码。
/// </summary>
public static class BidOpsBackgroundJobTypes
{
    /// <summary>
    /// 手工 URL 导入任务。
    /// </summary>
    public const string ManualUrlImport = "bidops.raw.manual-url-import";
    /// <summary>
    /// 原始附件回填任务。
    /// </summary>
    public const string RawAttachmentBackfill = "bidops.raw.attachment-backfill";
    /// <summary>
    /// 模拟来源采集任务。
    /// </summary>
    public const string MockCrawl = "bidops.crawl.mock-scan";
    /// <summary>
    /// 国家电网 ECP 公开站点采集任务。
    /// </summary>
    public const string StateGridEcpCrawl = "bidops.crawl.state-grid-ecp-scan";
    /// <summary>
    /// 附件下载与文本抽取任务。
    /// </summary>
    public const string AttachmentProcess = "bidops.document.attachment-process";
    /// <summary>
    /// 结构化解析任务。
    /// </summary>
    public const string StructuredParse = "bidops.ai.structured-parse";
    /// <summary>
    /// 模拟 AI 解析任务。
    /// </summary>
    public const string MockAiParse = "bidops.ai.mock-parse";
    /// <summary>
    /// 商机价值评估任务。
    /// </summary>
    public const string OpportunityValueAssessment = "bidops.opportunity.value-assessment";
    /// <summary>
    /// 商机截止时间提醒任务。
    /// </summary>
    public const string OpportunityDeadlineReminder = "bidops.opportunity.deadline-reminder";
    /// <summary>
    /// 商机关注提醒任务。
    /// </summary>
    public const string OpportunityWatchReminder = "bidops.opportunity.watch-reminder";
    /// <summary>
    /// 商机陈旧状态扫描任务。
    /// </summary>
    public const string OpportunityStaleStateScan = "bidops.opportunity.stale-state-scan";
    /// <summary>
    /// 供应商证明材料到期扫描任务。
    /// </summary>
    public const string SupplierEvidenceExpiryScan = "bidops.supplier.evidence-expiry-scan";
    /// <summary>
    /// 供应商匹配任务。
    /// </summary>
    public const string SupplierMatchRun = "bidops.matching.supplier-match-run";
    /// <summary>
    /// 结果公告供应商抽取任务。
    /// </summary>
    public const string OutcomeSupplierExtract = "bidops.outcome.supplier-extract";
    /// <summary>
    /// 结果公告供应商重建 dry-run 任务。
    /// </summary>
    public const string OutcomeSupplierRebuildDryRun = "bidops.outcome.supplier-rebuild-dry-run";
    /// <summary>
    /// 审核质量回填任务。
    /// </summary>
    public const string ReviewQualityBackfill = "bidops.review.quality-backfill";
    /// <summary>
    /// 待审核池批量确认任务。
    /// </summary>
    public const string ReviewBulkApprove = "bidops.review.bulk-approve";
    /// <summary>
    /// 结果驱动闭环分析任务。
    /// </summary>
    public const string LifecycleReverseClosure = "bidops.lifecycle.reverse-closure";
    /// <summary>
    /// 闭环字段级补全任务。
    /// </summary>
    public const string LifecycleFieldEnrichment = "bidops.lifecycle.field-enrichment";
}

/// <summary>
/// BidOps 后台任务优先级枚举值。
/// </summary>
public static class BidOpsBackgroundJobPriorities
{
    /// <summary>
    /// 自动任务默认优先级。
    /// </summary>
    public const int Automatic = 0;
    /// <summary>
    /// 人工触发任务优先级。
    /// </summary>
    public const int Manual = 100;
}

/// <summary>
/// 采集来源类型枚举值。
/// </summary>
public static class BidOpsCrawlSourceTypes
{
    /// <summary>
    /// 模拟采集来源。
    /// </summary>
    public const string Mock = "Mock";
    /// <summary>
    /// 人工触发优先级或手工来源类型。
    /// </summary>
    public const string Manual = "Manual";
    /// <summary>
    /// 国家电网 ECP 公开采集来源。
    /// </summary>
    public const string StateGridEcp = "StateGridEcp";
}

/// <summary>
/// BidOps 系统级固定取值和运行配置键。
/// </summary>
public static class BidOpsSystemValues
{
    /// <summary>
    /// BidOps 模块名称。
    /// </summary>
    public const string ModuleName = "BidOps";
    /// <summary>
    /// AI 提供方运行配置键。
    /// </summary>
    public const string AiProviderRuntimeSettingKey = "ai.provider";
    /// <summary>
    /// Codex CLI 默认模型配置键。
    /// </summary>
    public const string CodexCliModelRuntimeSettingKey = "ai.codex-cli.model";
    /// <summary>
    /// Codex CLI 默认推理强度配置键。
    /// </summary>
    public const string CodexCliReasoningEffortRuntimeSettingKey = "ai.codex-cli.reasoning-effort";
    /// <summary>
    /// Codex CLI 复杂场景模型配置键。
    /// </summary>
    public const string CodexCliComplexModelRuntimeSettingKey = "ai.codex-cli.complex.model";
    /// <summary>
    /// Codex CLI 复杂场景推理强度配置键。
    /// </summary>
    public const string CodexCliComplexReasoningEffortRuntimeSettingKey = "ai.codex-cli.complex.reasoning-effort";
    /// <summary>
    /// Codex CLI 手工重解析模型配置键。
    /// </summary>
    public const string CodexCliManualReparseModelRuntimeSettingKey = "ai.codex-cli.manual-reparse.model";
    /// <summary>
    /// Codex CLI 手工重解析推理强度配置键。
    /// </summary>
    public const string CodexCliManualReparseReasoningEffortRuntimeSettingKey = "ai.codex-cli.manual-reparse.reasoning-effort";
    /// <summary>
    /// Codex CLI 审核人提示词模型配置键。
    /// </summary>
    public const string CodexCliReviewerPromptModelRuntimeSettingKey = "ai.codex-cli.reviewer-prompt.model";
    /// <summary>
    /// Codex CLI 审核人提示词推理强度配置键。
    /// </summary>
    public const string CodexCliReviewerPromptReasoningEffortRuntimeSettingKey = "ai.codex-cli.reviewer-prompt.reasoning-effort";
    /// <summary>
    /// DeepSeek API Key 运行配置键。
    /// </summary>
    public const string DeepSeekApiKeyRuntimeSettingKey = "ai.deepseek.api-key";
    /// <summary>
    /// Mimo API Key 运行配置键。
    /// </summary>
    public const string MimoApiKeyRuntimeSettingKey = "ai.mimo.api-key";
    /// <summary>
    /// BidOps 任务暂停开关运行配置键。
    /// </summary>
    public const string TaskPauseRuntimeSettingKey = "runtime.task-pause";
    /// <summary>
    /// DeepSeek AI 提供方。
    /// </summary>
    public const string AiProviderDeepSeek = "DeepSeek";
    /// <summary>
    /// Mimo AI 提供方。
    /// </summary>
    public const string AiProviderMimo = "Mimo";
    /// <summary>
    /// Codex CLI AI 提供方。
    /// </summary>
    public const string AiProviderCodexCli = "CodexCli";
    /// <summary>
    /// DeepSeek 默认接口地址。
    /// </summary>
    public const string DefaultDeepSeekBaseUrl = "https://api.deepseek.com";
    /// <summary>
    /// DeepSeek 默认模型。
    /// </summary>
    public const string DefaultDeepSeekModel = "deepseek-v4-pro";
    /// <summary>
    /// Mimo 默认接口地址。
    /// </summary>
    public const string DefaultMimoBaseUrl = "https://token-plan-cn.xiaomimimo.com/v1";
    /// <summary>
    /// Mimo 默认模型。
    /// </summary>
    public const string DefaultMimoModel = "mimo-v2.5-pro";
    /// <summary>
    /// Codex CLI 默认模型。
    /// </summary>
    public const string DefaultCodexCliModel = "gpt-5.5";
    /// <summary>
    /// Codex CLI 默认推理强度。
    /// </summary>
    public const string DefaultCodexCliReasoningEffort = "low";
    /// <summary>
    /// Codex CLI 复杂场景默认推理强度。
    /// </summary>
    public const string DefaultCodexCliComplexReasoningEffort = "medium";
    /// <summary>
    /// Codex CLI 手工重解析默认推理强度。
    /// </summary>
    public const string DefaultCodexCliManualReparseReasoningEffort = "medium";
    /// <summary>
    /// Codex CLI 审核人提示词默认推理强度。
    /// </summary>
    public const string DefaultCodexCliReviewerPromptReasoningEffort = "xhigh";
    /// <summary>
    /// 手工导入来源编码。
    /// </summary>
    public const string ManualSourceCode = "manual";
    /// <summary>
    /// 模拟公开来源编码。
    /// </summary>
    public const string MockSourceCode = "mock-public";
    /// <summary>
    /// 国家电网 ECP 来源编码。
    /// </summary>
    public const string StateGridEcpSourceCode = "state-grid-ecp";
    /// <summary>
    /// 本地文件存储提供方。
    /// </summary>
    public const string LocalStorageProvider = "Local";
    /// <summary>
    /// 当前结构化解析器版本。
    /// </summary>
    public const string StructuredParserVersion = "v2";
}

/// <summary>
/// Codex CLI AI 场景枚举值。
/// </summary>
public static class BidOpsCodexCliScenarios
{
    /// <summary>
    /// 默认场景。
    /// </summary>
    public const string Default = "default";
    /// <summary>
    /// 复杂解析场景。
    /// </summary>
    public const string Complex = "complex";
    /// <summary>
    /// 手工重解析场景。
    /// </summary>
    public const string ManualReparse = "manual-reparse";
    /// <summary>
    /// 审核人补充提示词。
    /// </summary>
    public const string ReviewerPrompt = "reviewer-prompt";
}
