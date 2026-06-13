using System.IO.Compression;
using System.Reflection;
using System.Text;
using Atlas.BackgroundTasks;
using Atlas.Core.Authorization;
using Atlas.Extensions.DependencyInjection;
using Atlas.Modules.BidOps;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Controllers;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.EntityConfigurations;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

namespace Atlas.Services.Tests;

public sealed class BidOpsModuleTests
{
    [Fact]
    public void BidOpsModule_DeclaresEntityConfigurationAssembly()
    {
        var module = new BidOpsModule();

        Assert.Contains(typeof(CrawlSourceConfiguration).Assembly, module.EntityConfigurationAssemblies);
    }

    [Fact]
    public void BidOpsModule_RegistersServicesAndBackgroundHandlers()
    {
        var services = new ServiceCollection();
        var module = new BidOpsModule();
        module.AddServices(new AtlasModuleContext(services, new ConfigurationBuilder().Build(), module));

        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsCrawlService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsReviewService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOpportunityService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOpportunityMaintenanceService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsSupplierService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOutcomeSupplierExtractionService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsSupplierMaintenanceService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsMatchingService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsPursuitService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOperationsQueryService));
        Assert.Contains(services, x => x.ServiceType == typeof(IStateGridEcpCrawler));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsAttachmentProcessingService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsTextExtractor));
        Assert.True(services.Count(x => x.ServiceType == typeof(IBackgroundJobHandler)) >= 14);
        Assert.True(services.Count(x => x.ServiceType == typeof(IRecurringTask)) >= 4);
    }

    [Fact]
    public void BidOpsModule_DeclaresPermissionsAndMenus()
    {
        var builder = new AtlasAuthorizationCatalogBuilder("BidOpsTest");
        new BidOpsModule().ConfigureAuthorization(builder);

        var catalog = builder.Build();

        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.CrawlRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.DashboardRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.ReviewApprove));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.OpsRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.OpsManage));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.OpportunityRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.OpportunityManage));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.SupplierRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.SupplierManage));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.SupplierEvidenceRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.SupplierEvidenceManage));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.MatchingRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.MatchingRun));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.MatchingDecide));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.PursuitRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.PursuitManage));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.PursuitTaskManage));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.PursuitFollowRecordManage));
        Assert.True(catalog.MenuItems.ContainsKey("bidops"));
        Assert.True(catalog.MenuItems.ContainsKey("bidops.dashboard"));
        Assert.True(catalog.MenuItems.ContainsKey("bidops.review"));
        Assert.True(catalog.MenuItems.ContainsKey("bidops.opportunities"));
        Assert.True(catalog.MenuItems.ContainsKey("bidops.suppliers"));
        Assert.True(catalog.MenuItems.ContainsKey("bidops.matching"));
        Assert.True(catalog.MenuItems.ContainsKey("bidops.pursuits"));
        Assert.True(catalog.MenuItems.ContainsKey("bidops.operations"));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.CrawlRunLog));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Dashboard));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.RawNotice));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Opportunity));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Supplier));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.SupplierEvidence));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.OutcomeSupplierRecord));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Matching));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.GoNoGoDecision));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Pursuit));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.PursuitTask));
    }

    [Fact]
    public void OperationsControllers_DeclareP0Routes()
    {
        var opsRoute = typeof(BackgroundJobsOperationsController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var summaryRoute = typeof(BackgroundJobsOperationsController)
            .GetMethod(nameof(BackgroundJobsOperationsController.SummaryAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var bidOpsRoute = typeof(BidOpsOperationsController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var dashboardRoute = typeof(BidOpsOperationsController)
            .GetMethod(nameof(BidOpsOperationsController.DashboardAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var channelHealthRoute = typeof(BidOpsOperationsController)
            .GetMethod(nameof(BidOpsOperationsController.ChannelHealthAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var rawNoticePipelineRoute = typeof(BidOpsOperationsController)
            .GetMethod(nameof(BidOpsOperationsController.RawNoticePipelineAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var workersRoute = typeof(BackgroundWorkersOperationsController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var workersListRoute = typeof(BackgroundWorkersOperationsController)
            .GetMethod(nameof(BackgroundWorkersOperationsController.SearchAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/ops/background-jobs", opsRoute);
        Assert.Equal("summary", summaryRoute);
        Assert.Equal("api/bidops/operations", bidOpsRoute);
        Assert.Equal("dashboard", dashboardRoute);
        Assert.Equal("channels/health", channelHealthRoute);
        Assert.Equal("raw-notices/{id:long}/pipeline", rawNoticePipelineRoute);
        Assert.Equal("api/ops/workers", workersRoute);
        Assert.Null(workersListRoute);
    }

    [Fact]
    public void BidOpsDashboardController_DeclaresSummaryRoute()
    {
        var controllerRoute = typeof(BidOpsDashboardController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var summaryRoute = typeof(BidOpsDashboardController)
            .GetMethod(nameof(BidOpsDashboardController.SummaryAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/dashboard", controllerRoute);
        Assert.Equal("summary", summaryRoute);
    }

    [Fact]
    public void RawNoticesController_DeclaresPipelineAndReparseRoutes()
    {
        var controllerRoute = typeof(RawNoticesController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var pipelineRoute = typeof(RawNoticesController)
            .GetMethod(nameof(RawNoticesController.PipelineAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var reparseRoute = typeof(RawNoticesController)
            .GetMethod(nameof(RawNoticesController.ReparseAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var importUrlRoute = typeof(RawNoticesController)
            .GetMethod(nameof(RawNoticesController.ImportUrlAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var backfillAttachmentsRoute = typeof(RawNoticesController)
            .GetMethod(nameof(RawNoticesController.BackfillAttachmentsAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var attachmentFileRoute = typeof(RawNoticesController)
            .GetMethod(nameof(RawNoticesController.GetAttachmentFileAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/raw-notices", controllerRoute);
        Assert.Equal("{id:long}/pipeline", pipelineRoute);
        Assert.Equal("{id:long}/reparse", reparseRoute);
        Assert.Equal("import-url", importUrlRoute);
        Assert.Equal("backfill-attachments", backfillAttachmentsRoute);
        Assert.Equal("{id:long}/attachments/{attachmentId:long}/file", attachmentFileRoute);
    }

    [Fact]
    public void OpportunitiesController_DeclaresRoutes()
    {
        var controllerRoute = typeof(OpportunitiesController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var listRoute = typeof(OpportunitiesController)
            .GetMethod(nameof(OpportunitiesController.SearchAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var getRoute = typeof(OpportunitiesController)
            .GetMethod(nameof(OpportunitiesController.GetAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var createRoute = typeof(OpportunitiesController)
            .GetMethod(nameof(OpportunitiesController.CreateAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var watchRoute = typeof(OpportunitiesController)
            .GetMethod(nameof(OpportunitiesController.WatchAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var assessRoute = typeof(OpportunitiesController)
            .GetMethod(nameof(OpportunitiesController.AssessAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var stageRoute = typeof(OpportunitiesController)
            .GetMethod(nameof(OpportunitiesController.ChangeStageAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/opportunities", controllerRoute);
        Assert.Null(listRoute);
        Assert.Equal("{id:long}", getRoute);
        Assert.Null(createRoute);
        Assert.Equal("{id:long}/watch", watchRoute);
        Assert.Equal("{id:long}/assess", assessRoute);
        Assert.Equal("{id:long}/stage", stageRoute);
    }

    [Fact]
    public void SuppliersController_DeclaresRoutes()
    {
        var controllerRoute = typeof(SuppliersController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var listRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.SearchAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var getRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.GetAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var analysisRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.AnalysisSummaryAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var outcomeRecordsRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.OutcomeRecordsAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var outcomeSummaryRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.OutcomeSummaryAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var backfillRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.BackfillOutcomeRecordsAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var createRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.CreateAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var updateRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.UpdateAsync))?
            .GetCustomAttributes<HttpPutAttribute>()
            .SingleOrDefault()?
            .Template;
        var contactRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.AddContactAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var capabilityRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.AddCapabilityAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var evidenceRoute = typeof(SuppliersController)
            .GetMethod(nameof(SuppliersController.AddEvidenceDocumentAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/suppliers", controllerRoute);
        Assert.Null(listRoute);
        Assert.Equal("analysis/summary", analysisRoute);
        Assert.Equal("outcome-records", outcomeRecordsRoute);
        Assert.Equal("outcome-summary", outcomeSummaryRoute);
        Assert.Equal("outcome-records/backfill", backfillRoute);
        Assert.Equal("{id:long}", getRoute);
        Assert.Null(createRoute);
        Assert.Equal("{id:long}", updateRoute);
        Assert.Equal("{id:long}/contacts", contactRoute);
        Assert.Equal("{id:long}/capabilities", capabilityRoute);
        Assert.Equal("{id:long}/evidence-documents", evidenceRoute);
    }

    [Fact]
    public void MatchingController_DeclaresRoutes()
    {
        var controllerRoute = typeof(MatchingController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var listRoute = typeof(MatchingController)
            .GetMethod(nameof(MatchingController.SearchAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var getRoute = typeof(MatchingController)
            .GetMethod(nameof(MatchingController.GetAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var resultsRoute = typeof(MatchingController)
            .GetMethod(nameof(MatchingController.ResultsAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/matching/runs", controllerRoute);
        Assert.Null(listRoute);
        Assert.Equal("{id:long}", getRoute);
        Assert.Equal("{id:long}/results", resultsRoute);
    }

    [Fact]
    public void PursuitsController_DeclaresRoutes()
    {
        var controllerRoute = typeof(PursuitsController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var listRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.SearchAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var createRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.CreateAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var getRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.GetAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var updateRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.UpdateAsync))?
            .GetCustomAttributes<HttpPutAttribute>()
            .SingleOrDefault()?
            .Template;
        var statusRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.ChangeStatusAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var tasksRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.TasksAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var createTaskRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.CreateTaskAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var updateTaskRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.UpdateTaskAsync))?
            .GetCustomAttributes<HttpPutAttribute>()
            .SingleOrDefault()?
            .Template;
        var followRecordsRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.FollowRecordsAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var createFollowRecordRoute = typeof(PursuitsController)
            .GetMethod(nameof(PursuitsController.CreateFollowRecordAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/pursuits", controllerRoute);
        Assert.Null(listRoute);
        Assert.Null(createRoute);
        Assert.Equal("{id:long}", getRoute);
        Assert.Equal("{id:long}", updateRoute);
        Assert.Equal("{id:long}/status", statusRoute);
        Assert.Equal("{id:long}/tasks", tasksRoute);
        Assert.Equal("{id:long}/tasks", createTaskRoute);
        Assert.Equal("{id:long}/tasks/{taskId:long}", updateTaskRoute);
        Assert.Equal("{id:long}/follow-records", followRecordsRoute);
        Assert.Equal("{id:long}/follow-records", createFollowRecordRoute);
    }

    [Fact]
    public void CrawlRunLogsController_DeclaresListAndDetailRoutes()
    {
        var controllerRoute = typeof(CrawlRunLogsController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var listRoute = typeof(CrawlRunLogsController)
            .GetMethod(nameof(CrawlRunLogsController.SearchAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var getRoute = typeof(CrawlRunLogsController)
            .GetMethod(nameof(CrawlRunLogsController.GetAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/crawl-run-logs", controllerRoute);
        Assert.Null(listRoute);
        Assert.Equal("{id:long}", getRoute);
    }

    [Fact]
    public void ProcessingFailuresController_DeclaresListRoute()
    {
        var controllerRoute = typeof(ProcessingFailuresController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var listRoute = typeof(ProcessingFailuresController)
            .GetMethod(nameof(ProcessingFailuresController.SearchAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/processing/failures", controllerRoute);
        Assert.Null(listRoute);
    }

    [Fact]
    public void PackagesController_DeclaresPackageDetailAndTimelineRoutes()
    {
        var controllerRoute = typeof(PackagesController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var getRoute = typeof(PackagesController)
            .GetMethod(nameof(PackagesController.GetAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var timelineRoute = typeof(PackagesController)
            .GetMethod(nameof(PackagesController.TimelineAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var matchRoute = typeof(PackagesController)
            .GetMethod(nameof(PackagesController.MatchSuppliersAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var decisionsRoute = typeof(PackagesController)
            .GetMethod(nameof(PackagesController.DecisionsAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var historicalSuppliersRoute = typeof(PackagesController)
            .GetMethod(nameof(PackagesController.HistoricalSuppliersAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var createDecisionRoute = typeof(PackagesController)
            .GetMethod(nameof(PackagesController.CreateDecisionAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/packages", controllerRoute);
        Assert.Equal("{id:long}", getRoute);
        Assert.Equal("{id:long}/timeline", timelineRoute);
        Assert.Equal("{id:long}/match-suppliers", matchRoute);
        Assert.Equal("{id:long}/decisions", decisionsRoute);
        Assert.Equal("{id:long}/historical-suppliers", historicalSuppliersRoute);
        Assert.Equal("{id:long}/decisions", createDecisionRoute);
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_ExtractsCandidateSuppliers()
    {
        const string text = """
国网测试项目推荐成交候选人公示
项目编号：872610
包件号：包1
第一成交候选人：山东甲设备有限公司
第二成交候选人：北京乙科技有限公司
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网测试项目推荐成交候选人公示",
            "CandidateAnnouncement",
            text);

        Assert.Equal(2, records.Count);
        Assert.Contains(records, x =>
            x.SupplierName == "山东甲设备有限公司" &&
            x.OutcomeType == BidOpsOutcomeTypes.Candidate &&
            x.Rank == 1 &&
            x.PackageNo == "包1" &&
            x.ProjectCode == "872610");
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_ExtractsAwardedSupplierAndAmount()
    {
        const string text = """
国网测试项目成交结果公告
包件号：包2 成交供应商：北京乙科技有限公司 成交金额：123.45万元
""";

        var record = Assert.Single(BidOpsOutcomeSupplierTextParser.Extract(
            "国网测试项目成交结果公告",
            "AwardAnnouncement",
            text));

        Assert.Equal("北京乙科技有限公司", record.SupplierName);
        Assert.Equal(BidOpsOutcomeTypes.Awarded, record.OutcomeType);
        Assert.Equal("包2", record.PackageNo);
        Assert.Equal(1234500m, record.AwardAmount);
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_ExtractsAwardedSuppliersFromOutcomeTable()
    {
        const string text = """
国网山东电力经济技术研究院 2026 年第二次服务授权框架协议公开谈判采购
成交结果公告
采购项目编号：SD26-FWSQ-KJ-JYY02
分标编号 包号 成交人
9001005-9999 包 01 山东省地质测绘院
9001005-9999 包 02 山东省地矿工程集团有限公司
9001005-9999 包 03 山东省地质测绘院
9001005-9999 包 04 通用技术集团工程设计有限公司
9001005-9999 包 05 山东大学
9001005-9999 包 06 山东黄河勘测设计研究院有限公司
9011005-3999 包 01 山东微视文化传媒有限公司
9011005-3999 包 02 山东微视文化传媒有限公司
采购人：国网山东省电力公司经济技术研究院
代理机构：山东诚信工程建设监理有限公司
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网山东电力经济技术研究院2026年第二次服务授权框架协议公开谈判采购成交结果公告",
            "AwardAnnouncement",
            text);

        Assert.Equal(8, records.Count);
        Assert.Contains(records, x =>
            x.SupplierName == "山东省地质测绘院" &&
            x.OutcomeType == BidOpsOutcomeTypes.Awarded &&
            x.LotNo == "9001005-9999" &&
            x.PackageNo == "包 01");
        Assert.Contains(records, x => x.SupplierName == "山东大学" && x.PackageNo == "包 05");
        Assert.DoesNotContain(records, x => x.SupplierName.Contains("山东诚信", StringComparison.Ordinal));
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_CleansPdfTableRowSupplierNames()
    {
        const string text = """
国网河南电力漯河供电公司2026年第二次服务授权框架竞争性谈判采购成交结果公告
分标名称 包号 包名称 成交人
综合服务 包 1 变电站房屋维修 山东中星安装工程有限公司
综合服务 包 8 市辖区保电服务 铁塔能源有限公司河南分公司
综合服务 包 2 临颍县供电公司零星
综合服务 包 2临颍公司 26 年外聘律
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网河南电力漯河供电公司2026年第二次服务授权框架竞争性谈判采购成交结果公告",
            "AwardAnnouncement",
            text);

        Assert.Contains(records, x => x.SupplierName == "山东中星安装工程有限公司");
        Assert.Contains(records, x => x.SupplierName == "铁塔能源有限公司河南分公司");
        Assert.DoesNotContain(records, x => x.SupplierName.Contains("房屋维修", StringComparison.Ordinal));
        Assert.DoesNotContain(records, x => x.SupplierName.Contains("临颍县供电公司", StringComparison.Ordinal));
        Assert.DoesNotContain(records, x => x.SupplierName.Contains("临颍公司", StringComparison.Ordinal));
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_ExtractsAwardAmountsFromOutcomeTableColumns()
    {
        const string text = """
国网浙江电力2026年第三次服务公开招标采购中标候选人名单公示
分标编号 包号 投标人名称 投标报价（万元） 评审得分
9001005-9999 包 01 杭州悦玛电力技术有限公司 112.325700 93.20
9001005-9999 包 02 山东大学 16.05 万元 91.00
9001005-9999 包 03 北京乙科技有限公司 97.50% 88.00
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网浙江电力2026年第三次服务公开招标采购中标候选人名单公示",
            "CandidateAnnouncement",
            text);

        Assert.Contains(records, x =>
            x.SupplierName == "杭州悦玛电力技术有限公司" &&
            x.AwardAmount == 1123257.00m);
        Assert.Contains(records, x =>
            x.SupplierName == "山东大学" &&
            x.AwardAmount == 160500.00m);
        Assert.Contains(records, x =>
            x.SupplierName == "北京乙科技有限公司" &&
            x.AwardAmount == null);
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_DoesNotTreatDiscountRateAsAwardAmount()
    {
        const string text = """
国网测试项目成交结果公告
包件号：包1 成交供应商：北京乙科技有限公司 报价：97.50%
""";

        var record = Assert.Single(BidOpsOutcomeSupplierTextParser.Extract(
            "国网测试项目成交结果公告",
            "AwardAnnouncement",
            text));

        Assert.Equal("北京乙科技有限公司", record.SupplierName);
        Assert.Null(record.AwardAmount);
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_DropsTruncatedCompanySuffixFragments()
    {
        const string text = """
国网浙江电力2026年第三次服务公开招标采购中标候选人名单公示
分标名称 包号 投标人名称
综合服务 包 1 有限公司 98.00%
综合服务 包 2 务有限公司 97.50%
综合服务 包 5 工程有限公司 112.325700
综合服务 包 6 杭州悦玛电力技术有限公司 98.00%
综合服务 包 7 技有限公司 97.20%
综合服务 包 8 周口龙润电力（集团 88.00%
综合服务 包 9 研究院有限公司 65.88 万元
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网浙江电力2026年第三次服务公开招标采购中标候选人名单公示",
            "CandidateAnnouncement",
            text);

        var record = Assert.Single(records);
        Assert.Equal("杭州悦玛电力技术有限公司", record.SupplierName);
        Assert.DoesNotContain(records, x => x.SupplierName is "有限公司" or "技有限公司" or "工程有限公司" or "务有限公司" or "研究院有限公司");
        Assert.DoesNotContain(records, x => x.SupplierName.Contains("（集团", StringComparison.Ordinal));
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_DoesNotExtractPublishOrgAsSupplier()
    {
        const string text = """
国网山东省电力公司2026年服务中标候选人公示
resultValue.notice.PUBLISH_ORG_NAME: 国网山东省电力公司
resultValue.notice.BID_AGT: 山东诚信工程建设监理有限公司
采购人：国网山东省电力公司
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网山东省电力公司2026年服务中标候选人公示",
            "CandidateAnnouncement",
            text);

        Assert.Empty(records);
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_DoesNotExtractAnnouncementIntroAsSupplier()
    {
        const string text = """
国网河南电力商丘供电公司 2026 年第一次服务授权批次竞争性谈判采购评审工作已经结束，现将评审委员会推荐的成交候选人予以公示。
包件号：包1
第一成交候选人：河南甲设备有限公司
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网河南电力商丘供电公司2026年第一次服务授权批次竞争性谈判采购成交候选人公示",
            "CandidateAnnouncement",
            text);

        var record = Assert.Single(records);
        Assert.Equal("河南甲设备有限公司", record.SupplierName);
    }

    [Fact]
    public void BidOpsOutcomeSupplierTextParser_DoesNotExtractTemplateInstructionsAsSupplier()
    {
        const string text = """
公开谈判采购成交供应商须知”（国家电网有限公司电子商务平台 EPC2.0 公共信息
请各中标人关注“湖南湘能创业项目管理有限公司”
中标人在邮寄时写明公司全称，并在包裹外显著位置注明
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网测试项目成交结果公告",
            "AwardAnnouncement",
            text);

        Assert.Empty(records);
    }

    [Fact]
    public void BidOpsContentHasher_NormalizesWhitespace()
    {
        var hasher = new BidOpsContentHasher();

        Assert.Equal(hasher.HashText("hello   world"), hasher.HashText("hello world"));
    }

    [Fact]
    public void BidOpsContentHasher_IncludesSpaFragmentInUrlHash()
    {
        var hasher = new BidOpsContentHasher();

        var first = hasher.HashUrl("https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606118496631844_2018032700291334");
        var second = hasher.HashUrl("https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606118496302701_2018032700291334");

        Assert.NotEqual(first, second);
    }

    [Fact]
    public void BidOpsContentHasher_UsesStableStateGridWinFileIdentity()
    {
        var hasher = new BidOpsContentHasher();

        var first = hasher.HashUrl("https://ecp.sgcc.com.cn/ecp2.0/ecpwcmcore/index/downLoadWinFile?filePath=token-a&fileName=%E6%88%90%E4%BA%A4%E7%BB%93%E6%9E%9C%E5%85%AC%E5%91%8A.pdf");
        var second = hasher.HashUrl("https://ecp.sgcc.com.cn/ecp2.0/ecpwcmcore/index/downLoadWinFile?filePath=token-b&fileName=%E6%88%90%E4%BA%A4%E7%BB%93%E6%9E%9C%E5%85%AC%E5%91%8A.pdf");
        var third = hasher.HashUrl("https://ecp.sgcc.com.cn/ecp2.0/ecpwcmcore/index/downLoadWinFile?filePath=token-c&fileName=%E5%80%99%E9%80%89%E4%BA%BA%E5%85%AC%E7%A4%BA.pdf");

        Assert.Equal(first, second);
        Assert.NotEqual(first, third);
    }

    [Fact]
    public void StateGridEcpHtmlParser_DiscoversPublicNoticeLinks()
    {
        var html = """
<html><body>
  <a href="/ecp2.0/portal/#/doc/doci-bid/2606118491000000_2018032700291334">国网北京市电力公司2026年服务类公开招标采购公告</a>
</body></html>
""";

        var notices = StateGridEcpHtmlParser.DiscoverNotices(
            html,
            new Uri("https://ecp.sgcc.com.cn/ecp2.0/portal/"),
            10);

        var notice = Assert.Single(notices);
        Assert.Equal("国网北京市电力公司2026年服务类公开招标采购公告", notice.Title);
        Assert.StartsWith("https://ecp.sgcc.com.cn/ecp2.0/portal/", notice.DetailUrl);
    }

    [Fact]
    public void StateGridEcpWcmParser_ParsesNoticeListAndDetail()
    {
        const string listJson = """
{
  "successful": true,
  "resultValue": {
    "noteList": [
      {
        "firstPageDocId": 2606118491258552,
        "noticeId": 2606118491031697,
        "doctype": "doci-change",
        "title": "国网北京市电力公司2026年服务类第三次公开招标采购变更公告1",
        "noticePublishTime": "2026-06-11",
        "firstPageMenuId": 2018032700291334,
        "publishOrgName": "国网北京市电力公司",
        "code": "022673"
      }
    ]
  }
}
""";

        var notices = StateGridEcpWcmParser.ParseNoticeList(
            listJson,
            "https://ecp.sgcc.com.cn/ecp2.0/portal/",
            10);

        var notice = Assert.Single(notices);
        Assert.Equal(2606118491031697, notice.NoticeId);
        Assert.Equal("doci-change", notice.Doctype);
        Assert.Contains("2606118491031697_2018032700291334", notice.DetailUrl);
        Assert.Contains("2018032700291334", notice.DetailUrl);

        const string detailJson = """
{
  "successful": true,
  "resultValue": {
    "chgNotice": {
      "PURPRJ_NAME": "国网北京市电力公司2026年服务类第三次公开招标采购变更公告1",
      "PUBLISH_ORG_NAME": "国网北京市电力公司",
      "PURPRJ_CODE": "022673",
      "CHG_NOTICE_CONT": "详见附件",
      "PUB_TIME": "2026-06-11"
    }
  }
}
""";

        var document = StateGridEcpWcmParser.ParseNoticeDetail(detailJson, notice);

        Assert.Equal("国网北京市电力公司2026年服务类第三次公开招标采购变更公告1", document.Title);
        Assert.Contains("CHG_NOTICE_CONT", document.Text);
        Assert.Equal(new DateTime(2026, 6, 11), document.PublishTime);
    }

    [Fact]
    public void StateGridEcpWcmParser_PreservesDistinctPortalDetailFragments()
    {
        const string listJson = """
{
  "successful": true,
  "resultValue": {
    "noteList": [
      {
        "firstPageDocId": 2606118496631844,
        "noticeId": 2606118496575576,
        "doctype": "doci-bid",
        "title": "国网重庆市电力公司2026年第三次服务公开招标采购",
        "noticePublishTime": "2026-06-11",
        "firstPageMenuId": 2018032700291334,
        "publishOrgName": "国网重庆市电力公司",
        "code": "2026F3"
      },
      {
        "firstPageDocId": 2606118496302701,
        "noticeId": 2606118496122840,
        "doctype": "doci-bid",
        "title": "国网重庆市电力公司2026年第三次服务框架协议公开招标采购",
        "noticePublishTime": "2026-06-11",
        "firstPageMenuId": 2018032700291334,
        "publishOrgName": "国网重庆市电力公司",
        "code": "202613"
      }
    ]
  }
}
""";

        var notices = StateGridEcpWcmParser.ParseNoticeList(
            listJson,
            "https://ecp.sgcc.com.cn/ecp2.0/portal/",
            10);
        var hasher = new BidOpsContentHasher();

        Assert.Equal(2, notices.Count);
        Assert.NotEqual(notices[0].DetailUrl, notices[1].DetailUrl);
        Assert.NotEqual(hasher.HashUrl(notices[0].DetailUrl), hasher.HashUrl(notices[1].DetailUrl));
    }

    [Fact]
    public void StateGridEcpWcmParser_ParsesPortalDetailUrl()
    {
        var parsed = StateGridEcpWcmParser.TryParsePortalDetailUrl(
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128544990232_2018032900295987",
            out var doctype,
            out var noticeId,
            out var menuId);

        Assert.True(parsed);
        Assert.Equal("doci-bid", doctype);
        Assert.Equal(2606128544990232, noticeId);
        Assert.Equal("2018032900295987", menuId);
    }

    [Fact]
    public void StateGridEcpWcmParser_UsesNoticeIdForProcurementDetailRoute()
    {
        const string listJson = """
{
  "successful": true,
  "resultValue": {
    "noteList": [
      {
        "firstPageDocId": 2606108454405981,
        "noticeId": 2606108454368935,
        "id": 2606108454368935,
        "doctype": "doci-win",
        "title": "国网山东电力经济技术研究院2026年第二次服务授权框架协议公开谈判采购成交结果公告",
        "noticePublishTime": "2026-06-10",
        "firstPageMenuId": 2018060501171111,
        "publishOrgName": "国网山东省电力公司经济技术研究院"
      }
    ]
  }
}
""";

        var notices = StateGridEcpWcmParser.ParseNoticeList(
            listJson,
            "https://ecp.sgcc.com.cn/ecp2.0/portal/",
            10);

        var notice = Assert.Single(notices);
        Assert.Equal(2606108454368935, notice.NoticeId);
        Assert.Equal(2606108454405981, notice.FirstPageDocId);
        Assert.Contains("/#/doc/doci-win/2606108454368935_2018060501171111", notice.DetailUrl);
        Assert.DoesNotContain("2606108454405981", notice.DetailUrl);
    }

    [Fact]
    public void StateGridEcpWcmParser_DiscoversPublicAttachments()
    {
        const string detailJson = """
{
  "successful": true,
  "resultValue": {
    "notice": {
      "PURPRJ_NAME": "国网测试项目",
      "PUB_TIME": "2026-06-11"
    },
    "files": [
      {
        "fileName": "招标文件.pdf",
        "fileUrl": "https://ecp.sgcc.com.cn/ecp2.0/ecpwcmcore/file/download?id=1.pdf",
        "fileSize": "1024"
      }
    ]
  }
}
""";

        var notice = new StateGridEcpApiNotice(
            "国网测试项目",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606118492696118_2018032700291334",
            "doci-bid",
            "2018032700291334",
            2606118492644013,
            2606118492696118,
            new DateTime(2026, 6, 11),
            "国网测试单位",
            "TEST-001");

        var document = StateGridEcpWcmParser.ParseNoticeDetail(detailJson, notice);

        var attachment = Assert.Single(document.Attachments);
        Assert.Equal("招标文件.pdf", attachment.FileName);
        Assert.Equal("pdf", attachment.FileType);
    }

    [Fact]
    public void StateGridEcpWcmParser_AddsBidNoticeDownloadWhenFileFlagIsSet()
    {
        const string detailJson = """
{
  "successful": true,
  "resultValue": {
    "fileFlag": "1",
    "notice": {
      "PURPRJ_NAME": "北京电力交易中心有限公司2026年第一次服务公开谈判采购",
      "NOTICE_TYPE_NAME": "采购公告",
      "PUB_TIME": "2026-06-12"
    }
  }
}
""";

        var notice = new StateGridEcpApiNotice(
            "北京电力交易中心有限公司2026年第一次服务公开谈判采购",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128544990232_2018032900295987",
            "doci-bid",
            "2018032900295987",
            2606128544990232,
            null,
            new DateTime(2026, 6, 12),
            "北京电力交易中心有限公司",
            "0711-26OTL07533027");

        var document = StateGridEcpWcmParser.ParseNoticeDetail(detailJson, notice);

        var attachment = Assert.Single(document.Attachments);
        Assert.Equal("zip", attachment.FileType);
        Assert.Contains("北京电力交易中心有限公司2026年第一次服务公开谈判采购", attachment.FileName);
        Assert.Contains("/index/downLoadBid", attachment.FileUrl);
        Assert.Contains("noticeId=2606128544990232", attachment.FileUrl);
    }

    [Fact]
    public void StateGridEcpWcmParser_AddsBidNoticeDownloadWhenFileFlagIsMissing()
    {
        const string detailJson = """
{
  "successful": true,
  "resultValue": {
    "notice": {
      "PURPRJ_NAME": "国网新源集团有限公司2026年临一批服务公开谈判采购",
      "NOTICE_TYPE_NAME": "采购公告",
      "PUB_TIME": "2026-06-12"
    }
  }
}
""";

        var notice = new StateGridEcpApiNotice(
            "国网新源集团有限公司2026年临一批服务公开谈判采购",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128525313769_2018032900295987",
            "doci-bid",
            "2018032900295987",
            2606128525313769,
            null,
            new DateTime(2026, 6, 12),
            "国网新源集团有限公司",
            "46263E");

        var document = StateGridEcpWcmParser.ParseNoticeDetail(detailJson, notice);

        var attachment = Assert.Single(document.Attachments);
        Assert.Equal("zip", attachment.FileType);
        Assert.Contains("国网新源集团有限公司2026年临一批服务公开谈判采购", attachment.FileName);
        Assert.Contains("/index/downLoadBid", attachment.FileUrl);
        Assert.Contains("noticeId=2606128525313769", attachment.FileUrl);
    }

    [Fact]
    public void StateGridEcpWcmParser_DoesNotAddBidNoticeDownloadWhenFileFlagIsFalse()
    {
        const string detailJson = """
{
  "successful": true,
  "resultValue": {
    "fileFlag": "0",
    "notice": {
      "PURPRJ_NAME": "国网测试采购公告"
    }
  }
}
""";

        var notice = new StateGridEcpApiNotice(
            "国网测试采购公告",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128525313769_2018032900295987",
            "doci-bid",
            "2018032900295987",
            2606128525313769,
            null,
            new DateTime(2026, 6, 12),
            "国网测试单位",
            "TEST");

        var document = StateGridEcpWcmParser.ParseNoticeDetail(detailJson, notice);

        Assert.Empty(document.Attachments);
    }

    [Fact]
    public void StateGridEcpWcmParser_FallbackKeepsBidNoticeDownloadCandidate()
    {
        var notice = new StateGridEcpApiNotice(
            "国网新源集团有限公司2026年临一批服务公开谈判采购",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-bid/2606128525313769_2018032900295987",
            "doci-bid",
            "2018032900295987",
            2606128525313769,
            null,
            new DateTime(2026, 6, 12),
            "国网新源集团有限公司",
            "46263E");

        var document = StateGridEcpWcmParser.CreateFallbackDocument(notice);

        var attachment = Assert.Single(document.Attachments);
        Assert.Equal("zip", attachment.FileType);
        Assert.Contains("noticeId=2606128525313769", attachment.FileUrl);
    }

    [Fact]
    public void StateGridEcpWcmParser_ParsesWinFileAttachmentList()
    {
        const string fileJson = """
{
  "successful": true,
  "resultValue": {
    "files": [
      {
        "PURPRJ_NOTICE_ATTACH_ID": 2606108456346991,
        "FILE_E_SIGN_PATH": "/online/purchasing_management/close_bid/202606/sign/file.sig",
        "FILE_PATH": "encrypted-file-path|encrypted-ticket",
        "FILE_NAME": "国网山东电科院2026年第三次服务授权公开谈判采购成交结果公告.pdf",
        "FILE_E_SIGN_NAME": "文件电子签名260610845634699120260610142343.sig"
      }
    ]
  },
  "resultHint": "",
  "errorPage": "",
  "type": ""
}
""";

        var notice = new StateGridEcpApiNotice(
            "国网山东电科院2026年第三次服务授权公开谈判采购成交结果公告",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606108456217237_2018060501171111",
            "doci-win",
            "2018060501171111",
            2606108456217237,
            2606108454405981,
            new DateTime(2026, 6, 10),
            "国网山东省电力公司电力科学研究院",
            string.Empty);

        var attachments = StateGridEcpWcmParser.ParseNoticeFileList(
            fileJson,
            notice,
            "https://ecp.sgcc.com.cn/ecp2.0/portal/");

        var attachment = Assert.Single(attachments);
        Assert.Equal("国网山东电科院2026年第三次服务授权公开谈判采购成交结果公告.pdf", attachment.FileName);
        Assert.Equal("pdf", attachment.FileType);
        Assert.Contains("/ecp2.0/ecpwcmcore/index/downLoadWinFile", attachment.FileUrl);
        Assert.Contains("filePath=encrypted-file-path", attachment.FileUrl);
        Assert.Contains("fileName=", attachment.FileUrl);
    }

    [Fact]
    public void BidOpsDeterministicNoticeParser_UsesStateGridFields()
    {
        const string text = """
国网吉林电力超高压公司2026年第一次物资授权公开招标采购（一事一授权）
resultValue.notice.PURPRJ_NAME: 国网吉林电力超高压公司2026年第一次物资授权公开招标采购（一事一授权）
resultValue.notice.PURPRJ_CODE: 23FG10
resultValue.notice.PUBLISH_ORG_NAME: 国网吉林省电力有限公司超高压公司
resultValue.notice.BID_AGT: 国网吉林省电力有限公司建设分公司（吉林省吉能电力工程咨询有限公司）
resultValue.notice.PUR_TYPE_NAME: 物资
resultValue.notice.NOTICE_TYPE_NAME: 招标公告
resultValue.notice.BID_ORG: 国网吉林省电力有限公司超高压公司
resultValue.notice.OPENBID_TIME: 2026-07-02 10:00:00
resultValue.notice.BIDBOOK_BUY_END_TIME: 2026-06-18 08:00:00
""";

        var extract = BidOpsDeterministicNoticeParser.Extract(
            "fallback",
            text);

        Assert.Equal("TenderAnnouncement", extract.NoticeType);
        Assert.Equal("23FG10", extract.ProjectCode);
        Assert.Equal("国网吉林省电力有限公司超高压公司", extract.BuyerName);
        Assert.Equal("国网吉林省电力有限公司建设分公司（吉林省吉能电力工程咨询有限公司）", extract.AgencyName);
        Assert.Equal("吉林", extract.Region);
        Assert.Equal(new DateTime(2026, 7, 2, 10, 0, 0), extract.OpenBidTime);
        Assert.DoesNotContain("MOCK", extract.ProjectCode);
        Assert.Contains(extract.Packages.Single().Requirements, x => x.RequirementType == "Deadline" && x.RiskLevel == "High");
    }

    [Fact]
    public void BidOpsDeterministicNoticeParser_DoesNotUseNextFieldAsBlankProjectCode()
    {
        const string text = """
国网测试公告
ProjectCode:
ListPublishTime: 2026-06-11
resultValue.notice.ORG_NAME: 国网测试单位
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        Assert.Equal(string.Empty, extract.ProjectCode);
    }

    [Fact]
    public void BidOpsDeterministicNoticeParser_ExtractsProjectCodeFromHtmlContent()
    {
        const string text = """
国网智慧车联网技术有限公司2026年服务第二次框架协议竞争性谈判采购预成交供应商公示
ProjectCode:
ListPublishTime: 2026-06-11
resultValue.notice.CONT: <p><b><span>采购编</span><span>号：</span></b><b><span>872610</span></b></p>
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        Assert.Equal("872610", extract.ProjectCode);
    }

    [Fact]
    public void BidOpsDeterministicNoticeParser_DoesNotCreateUnknownPackageMarkers()
    {
        const string text = """
国网测试公告
ProjectCode:
ListPublishTime: 2026-06-11
resultValue.notice.PURPRJ_NAME: 国网测试公告
resultValue.notice.BID_ORG: ??????20260612165454
resultValue.notice.BID_AGT: ？？
resultValue.notice.PACKAGE_NO: ???20260612165454
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);
        var package = extract.Packages.Single();

        Assert.Equal(string.Empty, extract.BuyerName);
        Assert.Equal(string.Empty, extract.AgencyName);
        Assert.Equal(string.Empty, package.PackageNo);
        Assert.Equal(string.Empty, package.LotNo);
        Assert.DoesNotContain("UNSPECIFIED", package.PackageNo);
    }

    [Fact]
    public void BidOpsDeterministicNoticeParser_ExtractsPackageNoFromChineseHtmlContent()
    {
        const string text = """
国网测试公告
resultValue.notice.PURPRJ_NAME: 国网测试公告
resultValue.notice.CONT: <p><span>包</span><span>件号：</span><span>包1</span></p><p><span>标段号：</span><span>SG-01</span></p>
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);
        var package = extract.Packages.Single();

        Assert.Equal("包1", package.PackageNo);
        Assert.Equal("SG-01", package.LotNo);
    }

    [Fact]
    public async Task BidOpsTextExtractor_ExtractsHtmlText()
    {
        var extractor = new BidOpsTextExtractor();
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<html><body><h1>招标公告</h1><p>资格要求</p></body></html>"));

        var text = await extractor.ExtractAsync(stream, "notice.html", "text/html");

        Assert.Contains("招标公告", text);
        Assert.Contains("资格要求", text);
    }

    [Fact]
    public async Task BidOpsTextExtractor_ExtractsXlsxWorksheetText()
    {
        var extractor = new BidOpsTextExtractor();
        await using var stream = CreateXlsx(
            "包件清单",
            [
                ["包件号", "技术规范", "数量", "最高限价"],
                ["包1", "10kV环网柜", "12", "300000"],
                ["包2", "电缆附件", "36", "180000"]
            ]);

        var text = await extractor.ExtractAsync(
            stream,
            "招标清单.xlsx",
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");

        Assert.Contains("Sheet: 包件清单", text);
        Assert.Contains("包件号", text);
        Assert.Contains("10kV环网柜", text);
        Assert.Contains("电缆附件", text);
        Assert.Contains("300000", text);
    }

    [Fact]
    public async Task BidOpsTextExtractor_ExtractsDocxTablesAsMarkdown()
    {
        var extractor = new BidOpsTextExtractor();
        await using var stream = CreateStateGridProcurementDocx();

        var text = await extractor.ExtractAsync(
            stream,
            "采购公告.docx",
            "application/vnd.openxmlformats-officedocument.wordprocessingml.document");

        Assert.Contains("## 表格 1：1 项目概况与采购范围", text);
        Assert.Contains("| 分标编号 | 分标名称 | 包号 | 包名称 | 采购范围 | 服务期/框架协议有效期 | 实施地点 |", text);
        Assert.Contains("| 362601-9011 | 零星服务 | 包1 | 2026年微信公众号运营项目 |", text);
        Assert.Contains("## 表格 2：2 响应供应商须满足如下专用资格要求", text);
        Assert.Contains("| 分标 | 包号 | 包名称 | 资质要求 | 业绩要求 | 人员要求 |", text);
        Assert.Contains("自2021年1月1日至首次响应截止日，响应供应商具有宣传服务业绩", text);
    }

    [Fact]
    public void BidOpsDeterministicNoticeParser_ParsesStateGridEcpProcurementAttachmentTables()
    {
        var text = BuildStateGridProcurementMarkdownSample(44);

        var extract = BidOpsDeterministicNoticeParser.Extract(
            "北京电力交易中心有限公司2026年第一次服务公开谈判采购",
            text);

        Assert.Equal("0711-26OTL07533027", extract.ProjectCode);
        Assert.Equal("北京电力交易中心有限公司", extract.BuyerName);
        Assert.Equal(44, extract.Packages.Count);

        var first = extract.Packages[0];
        Assert.Equal("362601-9011", first.LotNo);
        Assert.Equal("零星服务", first.LotName);
        Assert.Equal("包1", first.PackageNo);
        Assert.Equal("2026年微信公众号运营项目", first.PackageName);
        Assert.Equal("北京", first.DeliveryPlace);

        var requirements = extract.Packages.SelectMany(x => x.Requirements).ToList();
        Assert.Equal(54, requirements.Count);
        Assert.Equal(44, requirements.Count(x => x.RequirementType == "Performance"));
        Assert.Equal(3, requirements.Count(x => x.RequirementType == "Qualification"));
        Assert.Equal(7, requirements.Count(x => x.RequirementType == "JointVenture"));
        Assert.All(extract.Packages, package =>
            Assert.Contains(package.Requirements, requirement => requirement.RequirementType == "Performance"));
    }

    [Fact]
    public void BidOpsEcpProcurementTableParser_RecognizesScopeHeaderAliases()
    {
        const string text = """
国网测试公告
resultValue.notice.PURPRJ_NAME: 国网测试公告
## 表格 1：1 项目概况与采购范围
| 分标编号 | 分标名称 | 包号 | 包名称 | 项目内容 | 服务期限 | 服务地点 |
|---|---|---|---|---|---|---|
| 362601-9010 | 数字化服务 | 包2 | 电力交易平台数据交互规范设计支撑 | 平台规范设计支撑 | 自合同签订之日起至2026年12月31日 | 北京 |
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        var package = Assert.Single(extract.Packages);
        Assert.Equal("362601-9010", package.LotNo);
        Assert.Equal("数字化服务", package.LotName);
        Assert.Equal("包2", package.PackageNo);
        Assert.Equal("电力交易平台数据交互规范设计支撑", package.PackageName);
        Assert.Equal("自合同签订之日起至2026年12月31日", package.DeliveryPeriod);
        Assert.Equal("北京", package.DeliveryPlace);
    }

    [Fact]
    public void BidOpsEcpProcurementTableParser_PromotesTwoRowQualificationHeaders()
    {
        const string text = """
国网测试公告
## 表格 1：1 项目概况与采购范围
| 分标编号 | 分标名称 | 包号 | 包名称 | 采购范围 | 服务期/框架协议有效期 | 实施地点 |
|---|---|---|---|---|---|---|
| 362601-9011 | 零星服务 | 包1 | 2026年微信公众号运营项目 | 微信公众号运营 | 自合同签订日起至2026年12月31日止 | 北京 |

## 表格 2：2 响应供应商须满足如下专用资格要求
| 分标 | 包号 | 包名称 | 响应供应商专用资格要求 |  |  |
|---|---|---|---|---|---|
|  |  |  | 资质要求 | 业绩要求 | 人员要求 |
| 362601-9011零星服务 | 包1 | 2026年微信公众号运营项目 | 接受联合体响应 | 自2021年1月1日至首次响应截止日，响应供应商具有宣传服务业绩。 | / |
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        var package = Assert.Single(extract.Packages);
        Assert.Contains(package.Requirements, x => x.RequirementType == "JointVenture");
        Assert.Contains(package.Requirements, x => x.RequirementType == "Performance");
    }

    [Fact]
    public void BidOpsEcpProcurementTableParser_FillsBlankQualificationParentColumns()
    {
        const string text = """
国网测试公告
## 表格 1：1 项目概况与采购范围
| 分标编号 | 分标名称 | 包号 | 包名称 | 采购范围 | 服务期/框架协议有效期 | 实施地点 |
|---|---|---|---|---|---|---|
| 362601-9011 | 零星服务 | 包1 | 2026年微信公众号运营项目 | 微信公众号运营 | 自合同签订日起至2026年12月31日止 | 北京 |

## 表格 2：2 响应供应商须满足如下专用资格要求
|  |  |  | 资质要求 | 业绩要求 | 人员要求 |
|---|---|---|---|---|---|
| 362601-9011零星服务 | 包1 | 2026年微信公众号运营项目 | / | 自2021年1月1日至首次响应截止日，响应供应商具有宣传服务业绩。 | / |
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        var package = Assert.Single(extract.Packages);
        Assert.Contains(package.Requirements, x => x.RequirementType == "Performance");
    }

    [Fact]
    public async Task BidOpsTextExtractor_ExtractsZipEntriesRecursively()
    {
        var extractor = new BidOpsTextExtractor();
        await using var xlsx = CreateXlsx(
            "需求明细",
            [
                ["包件号", "资格条件"],
                ["包A", "须提供型式试验报告"]
            ]);
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(archive, "附件/需求明细.xlsx", xlsx.ToArray());
            AddZipEntry(archive, "附件/说明.txt", Encoding.UTF8.GetBytes("递交截止时间：2026-07-02 10:00"));
        }

        stream.Position = 0;
        var text = await extractor.ExtractAsync(stream, "公告附件.zip", "application/zip");

        Assert.Contains("Archive: 公告附件.zip", text);
        Assert.Contains("File: 附件/需求明细.xlsx", text);
        Assert.Contains("须提供型式试验报告", text);
        Assert.Contains("递交截止时间：2026-07-02 10:00", text);
    }

    [Fact]
    public void BidOpsRawNoticeTextFormatter_ConvertsStateGridFieldsToChineseDisplayText()
    {
        const string rawText = """
国网测试公告
SourceUrl: https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/1_2
Doctype: doci-win
ProjectCode:
ListPublishTime: 2026-06-11
resultValue.notice.PURPRJ_NAME: 国网测试公告
resultValue.notice.CONT: <p><b><span>采购编</span><span>号：</span></b><b><span>872610</span></b></p>
resultValue.notice.BIDAGT_ID: 2019112996603887
resultValue.notice.bidagtName: 山东诚信工程建设监理有限公司
""";

        var text = BidOpsRawNoticeTextFormatter.ToDisplayText(rawText);

        Assert.Contains("原始公告地址：https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/1_2", text);
        Assert.Contains("公告类型：中标/成交结果公告", text);
        Assert.Contains("列表发布时间：2026-06-11", text);
        Assert.Contains("公告内容：采购编号：872610", text);
        Assert.Contains("代理机构：山东诚信工程建设监理有限公司", text);
        Assert.DoesNotContain("resultValue", text);
        Assert.DoesNotContain("Doctype:", text);
        Assert.DoesNotContain("ProjectCode:", text);
        Assert.DoesNotContain("BIDAGT_ID", text);
        Assert.DoesNotContain("<span>", text);
    }

    [Fact]
    public async Task BidOpsTextExtractor_ExtractsPdfTextWithoutRawPdfObjects()
    {
        var builder = new PdfDocumentBuilder();
        var font = builder.AddStandard14Font(Standard14Font.Helvetica);
        var page = builder.AddPage(595, 842);
        page.AddText("Tender Notice 17FG05", 12, new PdfPoint(50, 780), font);
        page.AddText("Candidate Announcement", 12, new PdfPoint(50, 760), font);
        await using var stream = new MemoryStream(builder.Build());
        var extractor = new BidOpsTextExtractor();

        var text = await extractor.ExtractAsync(stream, "notice.pdf", "application/pdf");
        var normalized = text.Replace("\r\n", "\n", StringComparison.Ordinal);

        Assert.Contains("Tender Notice 17FG05", normalized);
        Assert.Contains("Candidate Announcement", normalized);
        Assert.Contains('\n', normalized);
        Assert.DoesNotContain("Tender Notice 17FG05 Candidate Announcement", normalized);
        Assert.DoesNotContain("endstream", normalized);
        Assert.DoesNotContain("endobj", normalized);
    }

    private static MemoryStream CreateXlsx(
        string sheetName,
        IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var stream = new MemoryStream();
        var sharedStrings = rows
            .SelectMany(x => x)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var sharedStringIndexes = sharedStrings
            .Select((value, index) => new { value, index })
            .ToDictionary(x => x.value, x => x.index, StringComparer.Ordinal);

        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            AddZipEntry(
                archive,
                "xl/workbook.xml",
                Encoding.UTF8.GetBytes($$"""
<?xml version="1.0" encoding="UTF-8"?>
<workbook xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" xmlns:r="http://schemas.openxmlformats.org/officeDocument/2006/relationships">
  <sheets>
    <sheet name="{{EscapeXml(sheetName)}}" sheetId="1" r:id="rId1" />
  </sheets>
</workbook>
"""));

            AddZipEntry(
                archive,
                "xl/_rels/workbook.xml.rels",
                Encoding.UTF8.GetBytes("""
<?xml version="1.0" encoding="UTF-8"?>
<Relationships xmlns="http://schemas.openxmlformats.org/package/2006/relationships">
  <Relationship Id="rId1" Type="http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet" Target="worksheets/sheet1.xml" />
</Relationships>
"""));

            AddZipEntry(
                archive,
                "xl/sharedStrings.xml",
                Encoding.UTF8.GetBytes($$"""
<?xml version="1.0" encoding="UTF-8"?>
<sst xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main" count="{{sharedStrings.Count}}" uniqueCount="{{sharedStrings.Count}}">
{{string.Join(Environment.NewLine, sharedStrings.Select(x => $"  <si><t>{EscapeXml(x)}</t></si>"))}}
</sst>
"""));

            AddZipEntry(
                archive,
                "xl/worksheets/sheet1.xml",
                Encoding.UTF8.GetBytes(CreateWorksheetXml(rows, sharedStringIndexes)));
        }

        stream.Position = 0;
        return stream;
    }

    private static MemoryStream CreateStateGridProcurementDocx()
    {
        var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            var documentXml = $$"""
<?xml version="1.0" encoding="UTF-8"?>
<w:document xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main">
  <w:body>
    {{CreateWordParagraphXml("采购公告附件")}}
    {{CreateWordParagraphXml("1 项目概况与采购范围")}}
    {{CreateWordTableXml([
        ["分标编号", "分标名称", "包号", "包名称", "采购范围", "服务期/框架协议有效期", "实施地点"],
        ["362601-9011", "零星服务", "包1", "2026年微信公众号运营项目", "本项目要求供应商按照交易中心信息发布要求，对微信内容进行审校。", "自合同签订日起至2026年12月31日止", "北京"]
    ])}}
    {{CreateWordParagraphXml("2 响应供应商须满足如下专用资格要求")}}
    {{CreateWordTableXml([
        ["分标", "包号", "包名称", "响应供应商专用资格要求", "", ""],
        ["", "", "", "资质要求", "业绩要求", "人员要求"],
        ["362601-9011零星服务", "包1", "2026年微信公众号运营项目", "/", "自2021年1月1日至首次响应截止日，响应供应商具有宣传服务业绩。", "/"]
    ])}}
  </w:body>
</w:document>
""";
            AddZipEntry(archive, "word/document.xml", Encoding.UTF8.GetBytes(documentXml));
        }

        stream.Position = 0;
        return stream;
    }

    private static string BuildStateGridProcurementMarkdownSample(int packageCount)
    {
        var builder = new StringBuilder();
        builder.AppendLine("resultValue.notice.PURPRJ_NAME: 北京电力交易中心有限公司2026年第一次服务公开谈判采购");
        builder.AppendLine("resultValue.notice.PURPRJ_CODE: 0711-26OTL07533027");
        builder.AppendLine("resultValue.notice.BID_ORG: 北京电力交易中心有限公司");
        builder.AppendLine("resultValue.notice.BID_AGT: 国网物资有限公司");
        builder.AppendLine("## 表格 1：1 项目概况与采购范围");
        builder.AppendLine("| 分标编号 | 分标名称 | 包号 | 包名称 | 采购范围 | 服务期/框架协议有效期 | 实施地点 |");
        builder.AppendLine("|---|---|---|---|---|---|---|");

        var packages = BuildStateGridPackageRows(packageCount);
        foreach (var package in packages)
        {
            builder.AppendLine(
                $"| {package.LotCode} | {package.LotName} | {package.PackageNo} | {package.PackageName} | {package.Scope} | {package.Period} | 北京 |");
        }

        builder.AppendLine();
        builder.AppendLine("## 表格 2：2 响应供应商须满足如下专用资格要求");
        builder.AppendLine("| 分标 | 包号 | 包名称 | 资质要求 | 业绩要求 | 人员要求 |");
        builder.AppendLine("|---|---|---|---|---|---|");
        for (var i = 0; i < packages.Count; i++)
        {
            var qualification = i switch
            {
                < 7 => "接受联合体响应",
                < 10 => "具有建设行政主管部门核发的工程设计综合资质",
                _ => "/"
            };
            builder.AppendLine(
                $"| {packages[i].LotCode}{packages[i].LotName} | {packages[i].PackageNo} | {packages[i].PackageName} | {qualification} | 自2021年1月1日至首次响应截止日，响应供应商具有宣传服务业绩。 | / |");
        }

        return builder.ToString();
    }

    private static IReadOnlyList<StateGridPackageRow> BuildStateGridPackageRows(int count)
    {
        var rows = new List<StateGridPackageRow>();
        for (var i = 1; i <= count; i++)
        {
            var lotCode = i <= 3 ? "362601-9011" : i <= 5 ? "362601-9009" : i <= 30 ? "362601-9010" : "362601-9012";
            var lotName = lotCode switch
            {
                "362601-9011" => "零星服务",
                "362601-9009" => "生产辅助技改大修",
                "362601-9010" => "数字化服务",
                _ => "综合服务"
            };
            var packageName = i switch
            {
                1 => "2026年微信公众号运营项目",
                2 => "2026年电力市场基础知识宣传培训资源开发服务",
                _ => $"测试采购服务项目{i}"
            };

            rows.Add(new StateGridPackageRow(
                lotCode,
                lotName,
                $"包{i}",
                packageName,
                $"采购范围{i}",
                "自合同签订之日起至2026年12月31日"));
        }

        return rows;
    }

    private static string CreateWordParagraphXml(string text)
    {
        return $"<w:p><w:r><w:t>{EscapeXml(text)}</w:t></w:r></w:p>";
    }

    private static string CreateWordTableXml(IReadOnlyList<IReadOnlyList<string>> rows)
    {
        var builder = new StringBuilder();
        builder.AppendLine("<w:tbl>");
        foreach (var row in rows)
        {
            builder.AppendLine("  <w:tr>");
            foreach (var cell in row)
            {
                builder.Append("    <w:tc>");
                builder.Append(CreateWordParagraphXml(cell));
                builder.AppendLine("</w:tc>");
            }

            builder.AppendLine("  </w:tr>");
        }

        builder.AppendLine("</w:tbl>");
        return builder.ToString();
    }

    private static string CreateWorksheetXml(
        IReadOnlyList<IReadOnlyList<string>> rows,
        IReadOnlyDictionary<string, int> sharedStringIndexes)
    {
        var builder = new StringBuilder();
        builder.AppendLine("""<?xml version="1.0" encoding="UTF-8"?>""");
        builder.AppendLine("""<worksheet xmlns="http://schemas.openxmlformats.org/spreadsheetml/2006/main">""");
        builder.AppendLine("  <sheetData>");
        for (var rowIndex = 0; rowIndex < rows.Count; rowIndex++)
        {
            builder.AppendLine($"""    <row r="{rowIndex + 1}">""");
            for (var cellIndex = 0; cellIndex < rows[rowIndex].Count; cellIndex++)
            {
                var cellRef = $"{(char)('A' + cellIndex)}{rowIndex + 1}";
                var sharedIndex = sharedStringIndexes[rows[rowIndex][cellIndex]];
                builder.AppendLine($"""      <c r="{cellRef}" t="s"><v>{sharedIndex}</v></c>""");
            }

            builder.AppendLine("    </row>");
        }

        builder.AppendLine("  </sheetData>");
        builder.AppendLine("</worksheet>");
        return builder.ToString();
    }

    private static void AddZipEntry(
        ZipArchive archive,
        string path,
        byte[] content)
    {
        var entry = archive.CreateEntry(path);
        using var entryStream = entry.Open();
        entryStream.Write(content);
    }

    private static string EscapeXml(string value)
    {
        return value
            .Replace("&", "&amp;", StringComparison.Ordinal)
            .Replace("<", "&lt;", StringComparison.Ordinal)
            .Replace(">", "&gt;", StringComparison.Ordinal)
            .Replace("\"", "&quot;", StringComparison.Ordinal)
            .Replace("'", "&apos;", StringComparison.Ordinal);
    }

    private sealed record StateGridPackageRow(
        string LotCode,
        string LotName,
        string PackageNo,
        string PackageName,
        string Scope,
        string Period);
}
