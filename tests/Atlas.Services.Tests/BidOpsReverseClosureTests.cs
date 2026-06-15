using System.Reflection;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Controllers;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Mvc;

namespace Atlas.Services.Tests;

public sealed class BidOpsReverseClosureTests
{
    [Theory]
    [InlineData("包1", "1")]
    [InlineData("包01", "1")]
    [InlineData("包一", "1")]
    [InlineData("分包1", "1")]
    [InlineData("分包编号1", "1")]
    [InlineData("标包1", "1")]
    public void BidOpsPackageNoNormalizer_NormalizesCommonAliases(string input, string expected)
    {
        Assert.Equal(expected, BidOpsPackageNoNormalizer.Normalize(input));
    }

    [Theory]
    [InlineData("86万元", "860000")]
    [InlineData("1,234,567.89元", "1234567.89")]
    [InlineData("86.5万", "865000")]
    [InlineData("人民币86万元", "860000")]
    public void BidOpsMoneyNormalizer_NormalizesYuanAmounts(string input, string expectedText)
    {
        var expected = decimal.Parse(expectedText);

        Assert.Equal(expected, BidOpsMoneyNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData("97.50%")]
    [InlineData("费率 1.5%")]
    [InlineData("评分 88.00 分")]
    public void BidOpsMoneyNormalizer_DoesNotTreatRatesOrScoresAsMoney(string input)
    {
        Assert.Null(BidOpsMoneyNormalizer.TryNormalize(input));
    }

    [Fact]
    public void BidOpsAwardEvidenceParser_ExtractsFullAwardTable()
    {
        var records = BidOpsAwardEvidenceParser.Extract([Doc("""
项目编号：P-001
| 项目编号 | 分标编号 | 分标名称 | 包号 | 包名称 | 项目单位 | 中标人 | 中标金额 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| P-001 | LOT-1 | 信息服务 | 包1 | 运维服务 | 国网北京电力 | 北京甲科技有限公司 | 86万元 |
""")]);

        var record = Assert.Single(records);
        Assert.Equal("P-001", record.ProjectCode);
        Assert.Equal("LOT-1", record.LotNo);
        Assert.Equal("信息服务", record.LotName);
        Assert.Equal("国网北京电力", record.ProjectUnit);
        Assert.Equal("1", record.NormalizedPackageNo);
        Assert.Equal("北京甲科技有限公司", record.AwardedSupplierName);
        Assert.Equal(860000m, record.AwardAmount);
        Assert.Equal("AwardNotice", record.AmountSource);
    }

    [Fact]
    public void BidOpsAwardEvidenceParser_ExtractsStateGridHtmlAwardTable()
    {
        var records = BidOpsAwardEvidenceParser.Extract([Doc("""
<p align="center"><b>中标公告</b></p>
<p align="center"><b>国家电网有限公司2026年特高压项目第二次服务公开招标采购</b></p>
<p align="center"><b>（招标编号：</b><b>0711-26OTL04213025</b><b>）</b></p>
<table border="1">
  <tbody>
    <tr>
      <td><p><b><span>分标编号</span></b></p></td>
      <td><p><b><span>分标名称</span></b></p></td>
      <td><p><b><span>包号</span></b></p></td>
      <td><p><b><span>中标状态</span></b></p></td>
      <td><p><b><span>项目单位</span></b></p></td>
      <td><p><b><span>中标人</span></b></p></td>
    </tr>
    <tr>
      <td><p><span>SG2674-9001-13028</span></p></td>
      <td><p><span>变电站土建施工</span></p></td>
      <td><p><span><span>包</span><span>1</span></span></p></td>
      <td><p><span>中标</span></p></td>
      <td><p><span>国网四川省电力公司</span></p></td>
      <td><p><span>中国电建集团江西省水电工程局有限公司</span></p></td>
    </tr>
    <tr>
      <td><p><span>SG2674-9001-13028</span></p></td>
      <td><p><span>变电站土建施工</span></p></td>
      <td><p><span><span>包</span><span>2</span></span></p></td>
      <td><p><span>中标</span></p></td>
      <td><p><span>国网四川省电力公司</span></p></td>
      <td><p><span>国网四川电力送变电建设有限公司</span></p></td>
    </tr>
  </tbody>
</table>
""", "国家电网有限公司2026年特高压项目第二次服务公开招标采购中标公告")]);

        Assert.Equal(2, records.Count);
        var first = records.Single(x => x.NormalizedPackageNo == "1");
        Assert.Equal("0711-26OTL04213025", first.ProjectCode);
        Assert.Equal("SG2674-9001-13028", first.LotNo);
        Assert.Equal("变电站土建施工", first.LotName);
        Assert.Equal("包1", first.PackageNo);
        Assert.Equal("国网四川省电力公司", first.ProjectUnit);
        Assert.Equal("中国电建集团江西省水电工程局有限公司", first.AwardedSupplierName);
        Assert.Null(first.AwardAmount);
        Assert.Equal("Missing", first.AmountSource);
    }

    [Fact]
    public void BidOpsAwardEvidenceParser_FillsProjectAndLotFromBodyWhenTableOmitsColumns()
    {
        var records = BidOpsAwardEvidenceParser.Extract([Doc("""
<p>采购编号：0711-26OTL04213025</p>
<p>分标编号：SG2674-9001-13028</p>
<p>分标名称：变电站土建施工</p>
<table class="MsoNormalTable" border="1">
  <tr>
    <td><p class="MsoNormal"><span>包号</span></p></td>
    <td><p class="MsoNormal"><span>中标状态</span></p></td>
    <td><p class="MsoNormal"><span>项目单位</span></p></td>
    <td><p class="MsoNormal"><span>中标人</span></p></td>
  </tr>
  <tr>
    <td><p class="MsoNormal"><span>包</span><span>1</span></p></td>
    <td><p class="MsoNormal"><span>中标</span></p></td>
    <td><p class="MsoNormal"><span>国网四川省电力公司</span></p></td>
    <td><p class="MsoNormal"><span>中国电建集团江西省水电工程局有限公司</span></p></td>
  </tr>
</table>
""", "国家电网有限公司2026年特高压项目第二次服务公开招标采购中标公告")]);

        var record = Assert.Single(records);
        Assert.Equal("0711-26OTL04213025", record.ProjectCode);
        Assert.Equal("SG2674-9001-13028", record.LotNo);
        Assert.Equal("变电站土建施工", record.LotName);
        Assert.Equal("包1", record.PackageNo);
        Assert.Equal("国网四川省电力公司", record.ProjectUnit);
        Assert.Equal("中国电建集团江西省水电工程局有限公司", record.AwardedSupplierName);
    }

    [Fact]
    public void BidOpsAwardEvidenceParser_ExtractsSparseAwardTable()
    {
        var records = BidOpsAwardEvidenceParser.Extract([Doc("""
项目编号：P-002
| 项目编号 | 包号 | 中标人 |
| --- | --- | --- |
| P-002 | 包2 | 上海乙工程有限公司 |
""")]);

        var record = Assert.Single(records);
        Assert.Equal("P-002", record.ProjectCode);
        Assert.Equal("2", record.NormalizedPackageNo);
        Assert.Equal("上海乙工程有限公司", record.AwardedSupplierName);
        Assert.Null(record.AwardAmount);
        Assert.Equal("Missing", record.AmountSource);
    }

    [Fact]
    public void BidOpsAwardEvidenceParser_ExtractsParagraphPackageSupplier()
    {
        var records = BidOpsAwardEvidenceParser.Extract([Doc("""
项目编号：P-003
包1：广州丙建设有限公司
""")]);

        var record = Assert.Single(records);
        Assert.Equal("1", record.NormalizedPackageNo);
        Assert.Equal("广州丙建设有限公司", record.AwardedSupplierName);
        Assert.True(record.Confidence < 0.8);
    }

    [Fact]
    public void BidOpsCandidateEvidenceParser_ExtractsFullCandidateTableWithFillDown()
    {
        var records = BidOpsCandidateEvidenceParser.Extract([Doc("""
项目编号：P-004
| 分标编号 | 包号 | 包名称 | 应答人 | 排名 | 最终报价 | 质量 | 工期 | 资格条件 | 评标情况 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| LOT-A | 包1 | 运维服务 | 北京甲科技有限公司 | 1 | 86万元 | 合格 | 30天 | 满足 | 综合第一 |
|  |  |  | 上海乙工程有限公司 | 2 | 90万元 | 合格 | 30天 | 满足 | 综合第二 |
""", "P-004候选人公示", "CandidateAnnouncement")]);

        Assert.Equal(2, records.Count);
        Assert.Contains(records, x =>
            x.SupplierName == "北京甲科技有限公司" &&
            x.Rank == 1 &&
            x.FinalQuoteAmount == 860000m);
        Assert.Contains(records, x =>
            x.SupplierName == "上海乙工程有限公司" &&
            x.LotNo == "LOT-A" &&
            x.NormalizedPackageNo == "1");
    }

    [Fact]
    public void BidOpsCandidateEvidenceParser_ExtractsCompactCandidateTable()
    {
        var records = BidOpsCandidateEvidenceParser.Extract([Doc("""
项目编号：P-005
| 分标编号 | 分包编号 | 推荐候选人 | 排序 | 报价 |
| --- | --- | --- | --- | --- |
| LOT-B | 分包1 | 深圳丁电力有限公司 | 第一 | 1234567.89元 |
""", "P-005候选人公示", "CandidateAnnouncement")]);

        var record = Assert.Single(records);
        Assert.Equal("LOT-B", record.LotNo);
        Assert.Equal("1", record.NormalizedPackageNo);
        Assert.Equal(1, record.Rank);
        Assert.Equal(1234567.89m, record.FinalQuoteAmount);
    }

    [Fact]
    public void BidOpsCandidateEvidenceParser_ExtractsHorizontalTop3Table()
    {
        var records = BidOpsCandidateEvidenceParser.Extract([Doc("""
项目编号：P-006
| 包号 | 第一候选人 | 第一报价 | 第二候选人 | 第二报价 | 第三候选人 | 第三报价 |
| --- | --- | --- | --- | --- | --- | --- |
| 包1 | A测试有限公司 | 10万元 | B测试有限公司 | 11万元 | C测试有限公司 | 12万元 |
""", "P-006候选人公示", "CandidateAnnouncement")]);

        Assert.Equal(3, records.Count);
        Assert.Contains(records, x => x.SupplierName == "A测试有限公司" && x.Rank == 1 && x.FinalQuoteAmount == 100000m);
        Assert.Contains(records, x => x.SupplierName == "C测试有限公司" && x.Rank == 3 && x.FinalQuoteAmount == 120000m);
    }

    [Fact]
    public void BidOpsTenderPackageEvidenceParser_ExtractsScopeBudgetAndQualificationTables()
    {
        var records = BidOpsTenderPackageEvidenceParser.Extract([Doc("""
项目编号：P-007
| 分标编号 | 分标名称 | 包号 | 包名称 | 采购范围 | 服务期 | 实施地点 |
| --- | --- | --- | --- | --- | --- | --- |
| LOT-C | 服务 | 包1 | 运维服务 | 变电站运维 | 12个月 | 北京 |

| 包号 | 包名称 | 预算金额 | 最高限价 |
| --- | --- | --- | --- |
| 包1 | 运维服务 | 150万元 | 160万元 |

| 分标编号 | 包号 | 包名称 | 资质要求 | 业绩要求 | 人员要求 |
| --- | --- | --- | --- | --- | --- |
| LOT-C | 包1 | 运维服务 | 电力资质 | 近三年业绩 | 项目经理 |
""", "P-007采购公告", "ProcurementAnnouncement")]);

        Assert.Contains(records, x => x.ScopeText == "变电站运维" && x.DeliveryPlace == "北京");
        Assert.Contains(records, x => x.BudgetAmount == 1500000m && x.MaxPrice == 1600000m);
        Assert.Contains(records, x => x.QualificationText == "电力资质" && x.PerformanceRequirement == "近三年业绩");
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_UsesCandidateFinalQuoteWhenAwardAmountMissing()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: null);
        var candidate = Candidate("包1", "北京甲科技有限公司", rank: 1, amount: 860000m);
        var tender = Tender("包1", budget: 1000000m);

        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug([award], [candidate], [tender]));

        Assert.Equal(860000m, closure.FinalAwardAmount);
        Assert.Equal("CandidateFinalQuote", closure.FinalAwardAmountSource);
        Assert.Contains("Awarded supplier matched candidate rank 1", closure.MatchReasons);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_ClosesSparseAwardThroughCandidatePackageContext()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: null);
        var candidate = Candidate("包1", "北京甲科技有限公司", rank: 1, amount: 860000m) with
        {
            LotNo = "LOT-A",
            PackageName = "运维服务"
        };

        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug([award], [candidate], []));

        Assert.Equal("LOT-A", closure.LotNo);
        Assert.Equal("运维服务", closure.PackageName);
        Assert.Contains("tender notice not found", closure.MissingFields);
        Assert.True(closure.RequiresManualReview);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_DoesNotCrossLinkSamePackageAcrossLots()
    {
        var awardA = Award("包1", "北京甲科技有限公司", amount: null) with { LotNo = "LOT-A" };
        var awardB = Award("包1", "上海乙工程有限公司", amount: null) with { LotNo = "LOT-B" };
        var candidateB = Candidate("包1", "上海乙工程有限公司", rank: 1, amount: 900000m) with { LotNo = "LOT-B" };

        var closures = BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug([awardA, awardB], [candidateB], []);

        var closureA = Assert.Single(closures, x => x.LotNo == "LOT-A");
        var closureB = Assert.Single(closures, x => x.LotNo == "LOT-B");
        Assert.Contains("candidate notice not found", closureA.MissingFields);
        Assert.Equal(900000m, closureB.FinalAwardAmount);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_ReportsAwardAmountMissing()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: null);
        var candidate = Candidate("包1", "北京甲科技有限公司", rank: 1, amount: null);

        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug([award], [candidate], []));

        Assert.Null(closure.FinalAwardAmount);
        Assert.Equal("Missing", closure.FinalAwardAmountSource);
        Assert.Contains("award amount missing", closure.MissingFields);
    }

    [Fact]
    public void LifecycleDebugController_DeclaresReverseClosureRoutes()
    {
        var controllerRoute = typeof(LifecycleDebugController)
            .GetCustomAttribute<RouteAttribute>()?
            .Template;
        var urlRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.ReverseCloseUrlAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var rawRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.ReverseCloseRawNoticeAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/lifecycle/debug", controllerRoute);
        Assert.Equal("reverse-close-url", urlRoute);
        Assert.Equal("reverse-close-raw-notice/{rawNoticeId:long}", rawRoute);
    }

    private static BidOpsEvidenceDocument Doc(
        string text,
        string title = "P-001成交结果公告",
        string noticeType = "AwardAnnouncement")
    {
        return new BidOpsEvidenceDocument(
            new EvidenceSourceRef(1, null, noticeType, "https://example.test/notice", null, null, null, null, null),
            title,
            noticeType,
            null,
            text);
    }

    private static AwardEvidence Award(string packageNo, string supplier, decimal? amount)
    {
        return new AwardEvidence(
            "P-008",
            "国网测试项目",
            null,
            null,
            null,
            packageNo,
            BidOpsPackageNoNormalizer.Normalize(packageNo),
            null,
            supplier,
            amount,
            amount.HasValue ? "AwardNotice" : "Missing",
            Source("AwardNotice"),
            0.88);
    }

    private static CandidateEvidence Candidate(string packageNo, string supplier, int? rank, decimal? amount)
    {
        return new CandidateEvidence(
            "P-008",
            "国网测试项目",
            null,
            packageNo,
            BidOpsPackageNoNormalizer.Normalize(packageNo),
            null,
            supplier,
            rank,
            amount,
            null,
            null,
            null,
            null,
            Source("CandidateNotice"),
            0.9);
    }

    private static TenderPackageEvidence Tender(string packageNo, decimal? budget)
    {
        return new TenderPackageEvidence(
            "P-008",
            "国网测试项目",
            null,
            null,
            packageNo,
            BidOpsPackageNoNormalizer.Normalize(packageNo),
            "运维服务",
            "Service",
            "变电站运维",
            budget,
            budget,
            null,
            null,
            null,
            "电力资质",
            null,
            null,
            Source("TenderAnnouncement"),
            0.9);
    }

    private static EvidenceSourceRef Source(string noticeType)
    {
        return new EvidenceSourceRef(1, null, noticeType, "https://example.test/notice", null, null, null, null, "evidence");
    }
}
