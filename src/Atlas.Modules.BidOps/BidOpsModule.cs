using System.Reflection;
using Atlas.BackgroundTasks;
using Atlas.Core.Authorization;
using Atlas.Core.Enums;
using Atlas.Extensions.DependencyInjection;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.BackgroundJobs;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Staging;
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
        context.Services.AddScoped<IBidOpsQueryService, BidOpsQueryService>();
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
        context.Services.AddSingleton<IBidOpsFileStore, LocalBidOpsFileStore>();
        context.Services.AddSingleton<IBidOpsTextExtractor, BidOpsTextExtractor>();
        context.Services.AddSingleton<BidOpsContentHasher>();
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IBackgroundJobHandler, ManualUrlImportJobHandler>());
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
            ServiceDescriptor.Scoped<IRecurringTask, BidOpsScheduledScanTask>());
        context.Services.TryAddEnumerable(
            ServiceDescriptor.Scoped<IRecurringTask, BidOpsRecoveryTask>());
    }

    public override void ConfigureAuthorization(AtlasAuthorizationCatalogBuilder builder)
    {
        builder
            .AddPackage("atlas.standard", "Atlas Standard", AtlasPackageType.Edition)
            .AddCapability(BidOpsCapabilities.Crawl, "标讯采集", "BidOps")
            .AddCapability(BidOpsCapabilities.Review, "标讯审核", "BidOps")
            .AddCapability(BidOpsCapabilities.Business, "商机包件", "BidOps")
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
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Crawl)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Review)
            .AddPackageCapability("atlas.standard", BidOpsCapabilities.Business)
            .AddMenuItem(
                "bidops",
                "招投标作业",
                "/bidops",
                icon: "ClipboardList",
                sortOrder: 300,
                visibleWhen: AtlasAuthorizationCondition.AnyOf(
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.CrawlRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.ReviewRead),
                    AtlasAuthorizationCondition.RequirePermission(BidOpsPermissionCodes.BusinessRead)))
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
            .AddDataResource(
                BidOpsDataResources.CrawlSource,
                "BidOps crawl source",
                entityType: typeof(CrawlSource).FullName,
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
                supportedScopes: new[] { AtlasDataScopeType.AllTenant });
    }
}
