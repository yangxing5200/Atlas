using System.Reflection;
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
        Assert.True(services.Count(x => x.ServiceType == typeof(IBackgroundJobHandler)) >= 13);
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
        var attachmentFileRoute = typeof(RawNoticesController)
            .GetMethod(nameof(RawNoticesController.GetAttachmentFileAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/raw-notices", controllerRoute);
        Assert.Equal("{id:long}/pipeline", pipelineRoute);
        Assert.Equal("{id:long}/reparse", reparseRoute);
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
}
