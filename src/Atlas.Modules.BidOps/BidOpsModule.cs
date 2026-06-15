using System.Reflection;
using Atlas.BackgroundTasks;
using Atlas.Core.Authorization;
using Atlas.Core.Enums;
using Atlas.Extensions.DependencyInjection;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.BackgroundJobs;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities.Buyers;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Matching;
using Atlas.Modules.BidOps.Entities.Opportunities;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Pursuits;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Suppliers;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Atlas.Modules.BidOps;

public sealed class BidOpsModule : AtlasModule
{
    public override string Name => "Atlas.Modules.BidOps";

    public override IReadOnlyCollection<Assembly> EntityConfigurationAssemblies => new[] { Assembly };

    public override void AddServices(AtlasModuleContext context)
    {
        context.Services.AddScoped<IBidOpsCrawlService, BidOpsCrawlService>();
        context.Services.AddScoped<IBidOpsRawIngestionService, BidOpsRawIngestionService>();
        context.Services.AddScoped<IBidOpsAiParsingService, BidOpsAiParsingService>();
        context.Services.AddScoped<IBidOpsReviewService, BidOpsReviewService>();
        context.Services.AddScoped<IBidOpsOpportunityService, BidOpsOpportunityService>();
        context.Services.AddScoped<IBidOpsOpportunityMaintenanceService, BidOpsOpportunityMaintenanceService>();
        context.Services.AddScoped<IBidOpsSupplierService, BidOpsSupplierService>();
        context.Services.AddScoped<IBidOpsOutcomeSupplierExtractionService, BidOpsOutcomeSupplierExtractionService>();
        context.Services.AddScoped<IBidOpsOrganizationMasterDataService, BidOpsOrganizationMasterDataService>();
        context.Services.AddScoped<IBidOpsSupplierMaintenanceService, BidOpsSupplierMaintenanceService>();
        context.Services.AddScoped<IBidOpsMatchingService, BidOpsMatchingService>();
        context.Services.AddScoped<IBidOpsPursuitService, BidOpsPursuitService>();
        context.Services.AddScoped<IBidOpsReverseLifecycleClosureService, BidOpsReverseLifecycleClosureService>();
        context.Services.AddScoped<IBidOpsQueryService, BidOpsQueryService>();
        context.Services.AddScoped<IBidOpsOperationsQueryService, BidOpsOperationsQueryService>();
        context.Services.AddHttpClient<IStateGridEcpCrawler, StateGridEcpCrawler>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        context.Services.AddHttpClient<IBidOpsAttachmentProcessingService, BidOpsAttachmentProcessingService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(3);
        });
        context.Services.AddHttpClient<IBidOpsAiExtractionService, BidOpsStructuredExtractionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        context.Services.AddHttpClient<IBidOpsOutcomeSupplierAiExtractionService, BidOpsOutcomeSupplierAiExtractionService>(client =>
        {
            client.Timeout = TimeSpan.FromMinutes(2);
        });
        context.Services.AddSingleton<IBidOpsFileStore, LocalBidOpsFileStore>();
        context.Services.AddSingleton<IBidOpsTextExtractor, BidOpsTextExtractor>();
        context.Services.AddSingleton<BidOpsContentHasher>();
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Singleton<IBidOpsCrawlAdapter, StateGridEcpCrawlAdapter>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, ManualUrlImportJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, RawAttachmentBackfillJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, MockCrawlJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, StateGridEcpCrawlJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, AttachmentProcessJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, StructuredParseJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, MockAiParseJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, OpportunityValueAssessmentJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, OpportunityDeadlineReminderJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, OpportunityWatchReminderJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, OpportunityStaleStateScanJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, SupplierEvidenceExpiryScanJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, SupplierMatchRunJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, OutcomeSupplierExtractJobHandler>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IRecurringTask, BidOpsScheduledScanTask>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IRecurringTask, BidOpsRecoveryTask>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IRecurringTask, BidOpsOpportunityMaintenanceTask>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IRecurringTask, BidOpsSupplierMaintenanceTask>());
    }

    public override void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
    {
        builder
            .AddPackage("atlas.standard", "Atlas Standard", AtlasPackageType.Edition)
            .AddCapability(BidOpsCapabilities.Dashboard, "指挥中心", "BidOps")
            .AddCapability(BidOpsCapabilities.Crawl, "标讯采集", "BidOps")
            .AddCapability(BidOpsCapabilities.Review, "标讯审核", "BidOps")
            .AddCapability(BidOpsCapabilities.Business, "商机包件", "BidOps")
            .AddCapability(BidOpsCapabilities.Opportunity, "商机经营", "BidOps")
            .AddCapability(BidOpsCapabilities.Supplier, "厂家能力", "BidOps")
            .AddCapability(BidOpsCapabilities.Matching, "匹配立项", "BidOps")
            .AddCapability(BidOpsCapabilities.Pursuit, "投标作业", "BidOps")
            .AddCapability(BidOpsCapabilities.Operations, "运维监控", "BidOps")
            .AddPermission(
                BidOpsPermissionCodes.DashboardRead,
                "Read BidOps dashboard",
                BidOpsCapabilities.Dashboard,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Dashboard,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.CrawlRead,
                "Read BidOps crawl data",
                BidOpsCapabilities.Crawl,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.RawNotice,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.CrawlManage,
                "Manage BidOps crawl sources",
                BidOpsCapabilities.Crawl,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.CrawlSource,
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.CrawlImport,
                "Import public tender URL",
                BidOpsCapabilities.Crawl,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.RawNotice,
                action: "import",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.ReviewRead,
                "Read BidOps review tasks",
                BidOpsCapabilities.Review,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.ReviewTask,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.ReviewApprove,
                "Approve BidOps staging data",
                BidOpsCapabilities.Review,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.ReviewTask,
                action: "approve",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                BidOpsPermissionCodes.BusinessRead,
                "Read BidOps formal tender data",
                BidOpsCapabilities.Business,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Notice,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.OpportunityRead,
                "Read BidOps opportunities",
                BidOpsCapabilities.Opportunity,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Opportunity,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.OpportunityManage,
                "Manage BidOps opportunities",
                BidOpsCapabilities.Opportunity,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Opportunity,
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.OpportunityWatch,
                "Watch BidOps opportunities",
                BidOpsCapabilities.Opportunity,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Opportunity,
                action: "watch")
            .AddPermission(
                BidOpsPermissionCodes.OpportunityAssess,
                "Assess BidOps opportunities",
                BidOpsCapabilities.Opportunity,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Opportunity,
                action: "assess",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.SupplierRead,
                "Read BidOps suppliers",
                BidOpsCapabilities.Supplier,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Supplier,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.SupplierManage,
                "Manage BidOps suppliers",
                BidOpsCapabilities.Supplier,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Supplier,
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.SupplierEvidenceRead,
                "Read BidOps supplier evidence",
                BidOpsCapabilities.Supplier,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.SupplierEvidence,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.SupplierEvidenceManage,
                "Manage BidOps supplier evidence",
                BidOpsCapabilities.Supplier,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.SupplierEvidence,
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.MatchingRead,
                "Read BidOps matching runs",
                BidOpsCapabilities.Matching,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Matching,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.MatchingRun,
                "Run BidOps supplier matching",
                BidOpsCapabilities.Matching,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Matching,
                action: "run",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.MatchingDecide,
                "Record BidOps go/no-go decisions",
                BidOpsCapabilities.Matching,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.GoNoGoDecision,
                action: "decide",
                riskLevel: AtlasPermissionRiskLevel.High)
            .AddPermission(
                BidOpsPermissionCodes.PursuitRead,
                "Read BidOps pursuits",
                BidOpsCapabilities.Pursuit,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Pursuit,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.PursuitManage,
                "Manage BidOps pursuits",
                BidOpsCapabilities.Pursuit,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Pursuit,
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.PursuitTaskManage,
                "Manage BidOps pursuit tasks",
                BidOpsCapabilities.Pursuit,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.PursuitTask,
                action: "task.manage",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.PursuitFollowRecordManage,
                "Manage BidOps pursuit follow records",
                BidOpsCapabilities.Pursuit,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Pursuit,
                action: "follow-record.manage",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPermission(
                BidOpsPermissionCodes.OpsRead,
                "Read BidOps operations data",
                BidOpsCapabilities.Operations,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Operation,
                action: "read")
            .AddPermission(
                BidOpsPermissionCodes.OpsManage,
                "Manage BidOps operations jobs",
                BidOpsCapabilities.Operations,
                BidOpsSystemValues.ModuleName,
                PermissionScope.Tenant,
                resource: BidOpsDataResources.Operation,
                action: "manage",
                riskLevel: AtlasPermissionRiskLevel.Medium)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Crawl)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Dashboard)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Review)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Business)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Opportunity)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Supplier)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Matching)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Pursuit)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Operations)
            .AddMenuItem(
                "bidops",
                "招投标作业",
                "/bidops",
                icon: "ClipboardList",
                sortOrder: 300,
                visibleWhen: AtlasAuthorizationCondition.AnyOf(
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.DashboardRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.CrawlRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.ReviewRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.BusinessRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.OpportunityRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.SupplierRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.MatchingRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.PursuitRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.OpsRead)))
            .AddMenuItem(
                "bidops.dashboard",
                "指挥中心",
                "/bidops/dashboard",
                parentCode: "bidops",
                icon: "LayoutDashboard",
                sortOrder: 305,
                visibleWhen: AtlasAuthorizationCondition.AnyOf(
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.DashboardRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.CrawlRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.ReviewRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.BusinessRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.OpportunityRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.SupplierRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.MatchingRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.PursuitRead)))
            .AddMenuItem(
                "bidops.crawl",
                "标讯采集",
                "/bidops/crawl",
                parentCode: "bidops",
                icon: "Globe",
                sortOrder: 310,
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.CrawlRead))
            .AddMenuItem(
                "bidops.review",
                "待审核池",
                "/bidops/review",
                parentCode: "bidops",
                icon: "ListChecks",
                sortOrder: 320,
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.ReviewRead))
            .AddMenuItem(
                "bidops.packages",
                "商机包件",
                "/bidops/packages",
                parentCode: "bidops",
                icon: "PackageSearch",
                sortOrder: 330,
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.BusinessRead))
            .AddMenuItem(
                "bidops.opportunities",
                "商机经营",
                "/bidops/opportunities",
                parentCode: "bidops",
                icon: "Target",
                sortOrder: 335,
                visibleWhen: AtlasAuthorizationCondition.AnyOf(
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.OpportunityRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.BusinessRead)))
            .AddMenuItem(
                "bidops.suppliers",
                "厂家能力",
                "/bidops/suppliers",
                parentCode: "bidops",
                icon: "Factory",
                sortOrder: 337,
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.SupplierRead))
            .AddMenuItem(
                "bidops.matching",
                "匹配立项",
                "/bidops/matching/runs",
                parentCode: "bidops",
                icon: "GitCompareArrows",
                sortOrder: 338,
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.MatchingRead))
            .AddMenuItem(
                "bidops.pursuits",
                "投标作业",
                "/bidops/pursuits",
                parentCode: "bidops",
                icon: "BriefcaseBusiness",
                sortOrder: 339,
                visibleWhen: AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.PursuitRead))
            .AddMenuItem(
                "bidops.operations",
                "运维监控",
                "/bidops/operations",
                parentCode: "bidops",
                icon: "Activity",
                sortOrder: 340,
                visibleWhen: AtlasAuthorizationCondition.AnyOf(
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.CrawlRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.OpsRead)))
            .AddDataResource(
                BidOpsDataResources.Dashboard,
                "BidOps dashboard",
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.CrawlSource,
                "BidOps crawl source",
                entityType: typeof(CrawlSource).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.CrawlRunLog,
                "BidOps crawl run log",
                entityType: typeof(CrawlRunLog).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.RawNotice,
                "BidOps raw notice",
                entityType: typeof(RawNotice).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.ReviewTask,
                "BidOps review task",
                entityType: typeof(ReviewTask).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.Notice,
                "BidOps notice",
                entityType: typeof(Notice).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.TenderPackage,
                "BidOps tender package",
                entityType: typeof(TenderPackage).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.Opportunity,
                "BidOps opportunity",
                entityType: typeof(Opportunity).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.Buyer,
                "BidOps buyer",
                entityType: typeof(Buyer).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.BuyerProcurement,
                "BidOps buyer procurement",
                entityType: typeof(BuyerProcurementRecord).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.Supplier,
                "BidOps supplier",
                entityType: typeof(Supplier).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.SupplierEvidence,
                "BidOps supplier evidence",
                entityType: typeof(SupplierEvidenceDocument).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.OutcomeSupplierRecord,
                "BidOps outcome supplier record",
                entityType: typeof(OutcomeSupplierRecord).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.Matching,
                "BidOps supplier matching",
                entityType: typeof(SupplierMatchRun).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.GoNoGoDecision,
                "BidOps go/no-go decision",
                entityType: typeof(GoNoGoDecision).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.Pursuit,
                "BidOps pursuit",
                entityType: typeof(Pursuit).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant })
            .AddDataResource(
                BidOpsDataResources.PursuitTask,
                "BidOps pursuit task",
                entityType: typeof(PursuitTask).FullName,
                supportedScopes: new[] { AtlasDataScopeType.AllTenant });
    }
}
