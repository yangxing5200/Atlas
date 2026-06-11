using Atlas.BackgroundTasks;
using Atlas.Core.Authorization;
using Atlas.Extensions.DependencyInjection;
using Atlas.Modules.BidOps;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.EntityConfigurations;
using Atlas.Modules.BidOps.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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
        Assert.Contains(services, x => x.ServiceType == typeof(IStateGridEcpCrawler));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsAttachmentProcessingService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsTextExtractor));
        Assert.True(services.Count(x => x.ServiceType == typeof(IBackgroundJobHandler)) >= 6);
        Assert.True(services.Count(x => x.ServiceType == typeof(IRecurringTask)) >= 2);
    }

    [Fact]
    public void BidOpsModule_DeclaresPermissionsAndMenus()
    {
        var builder = new AtlasAuthorizationCatalogBuilder("BidOpsTest");
        new BidOpsModule().ConfigureAuthorization(builder);

        var catalog = builder.Build();

        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.CrawlRead));
        Assert.True(catalog.Permissions.ContainsKey(BidOpsPermissionCodes.ReviewApprove));
        Assert.True(catalog.MenuItems.ContainsKey("bidops"));
        Assert.True(catalog.MenuItems.ContainsKey("bidops.review"));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.RawNotice));
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
    public async Task BidOpsTextExtractor_ExtractsHtmlText()
    {
        var extractor = new BidOpsTextExtractor();
        await using var stream = new MemoryStream(System.Text.Encoding.UTF8.GetBytes("<html><body><h1>招标公告</h1><p>资格要求</p></body></html>"));

        var text = await extractor.ExtractAsync(stream, "notice.html", "text/html");

        Assert.Contains("招标公告", text);
        Assert.Contains("资格要求", text);
    }
}
