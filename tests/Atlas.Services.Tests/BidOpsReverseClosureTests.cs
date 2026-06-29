using System.Reflection;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.Controllers;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Models;
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

    [Fact]
    public void BidOpsMoneyNormalizer_UsesTenThousandYuanUnitContextForUnitlessCells()
    {
        Assert.Equal(658800m, BidOpsMoneyNormalizer.TryNormalize("65.88", "成交金额（万元）"));
        Assert.Equal(658800m, BidOpsMoneyNormalizer.TryNormalize("65.88", "金额单位：万元"));
        var yuanColumnContext = BidOpsMoneyNormalizer.BuildUnitContext("成交金额（元）", "金额单位：万元");
        Assert.Equal(65.88m, BidOpsMoneyNormalizer.TryNormalize("65.88", yuanColumnContext));
    }

    [Theory]
    [InlineData("97.50%")]
    [InlineData("费率 1.5%")]
    [InlineData("评分 88.00 分")]
    public void BidOpsMoneyNormalizer_DoesNotTreatRatesOrScoresAsMoney(string input)
    {
        Assert.Null(BidOpsMoneyNormalizer.TryNormalize(input));
    }

    [Theory]
    [InlineData("折扣率90%", BidOpsRateTypes.DiscountRate, "0.9")]
    [InlineData("下浮率10%", BidOpsRateTypes.ReductionRate, "0.1")]
    [InlineData("报价系数0.92", BidOpsRateTypes.Coefficient, "0.92")]
    [InlineData("9折", BidOpsRateTypes.DiscountRate, "0.9")]
    [InlineData("比例90%", BidOpsRateTypes.Unknown, "0.9")]
    public void BidOpsRateNormalizer_ParsesSupportedRateSemantics(
        string input,
        string expectedType,
        string expectedRateText)
    {
        var expectedRate = decimal.Parse(expectedRateText);

        var rate = BidOpsRateNormalizer.TryNormalize(input, Source("AwardNotice"));

        Assert.NotNull(rate);
        Assert.Equal(expectedType, rate.RateType);
        Assert.Equal(expectedRate, rate.RateValue);
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
    public void BidOpsAwardEvidenceParser_UsesAmountHeaderTenThousandYuanUnit()
    {
        var records = BidOpsAwardEvidenceParser.Extract([Doc("""
项目编号：P-001
| 包号 | 成交人 | 成交金额（万元） |
| 包1 | 北京甲科技有限公司 | 65.88 |
""")]);

        var record = Assert.Single(records);
        Assert.Equal("包1", record.PackageNo);
        Assert.Equal("北京甲科技有限公司", record.AwardedSupplierName);
        Assert.Equal(658800m, record.AwardAmount);
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
    public void BidOpsOutcomeSupplierExtractBuilder_ExtractsStateGridWrappedAwardTableWithoutSequence()
    {
        var records = BidOpsOutcomeSupplierExtractBuilder.Extract(
            "国网辽宁经研院2026年增补第一次服务授权公开谈判采购成交结果公告",
            "AwardAnnouncement",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606108455546447_2018060501171111",
            new DateTime(2026, 6, 10),
            """
            成交结果公告
            （采购项目编号：22FK09）
            分标编号 分标名称 包号 包名称 成交供应商名称

            22FK09-9012

            008-T035
            科技项目-经研

            院科技科研 包 8
            国网辽宁经研院 2026 年辽宁电网

            高压嵌入式直流适应性研究科技

            项目
            东北电力大学

            22FK09-9012

            008-T035
            科技项目-经研

            院科技科研 包 9
            国网辽宁经研院 2026 年基于检索

            增强的评审标准智能解析与知识

            图谱构建科技项目
            中国科学院沈阳计算技术

            研究所有限公司

            22FK09-9012

            008-T035
            科技项目-经研

            院科技科研 包 12
            国网辽宁经研院 2026 年为适应工

            业用户绿电直连需求构建电网高

            效经济运行科技项目
            东北大学

            成交通知书及发票领取、成交供应商纸质响应文件的递交要求详见平台须知
            """,
            rawNoticeId: 327333277306327040);

        var package8 = Assert.Single(records.Where(x =>
            x.SupplierName == "东北电力大学" &&
            BidOpsPackageNoNormalizer.Normalize(x.PackageNo) == "8" &&
            x.LotNo == "22FK09-9012008-T035"));
        Assert.Equal("22FK09", package8.ProjectCode);
        Assert.Equal("科技项目-经研院科技科研", package8.LotName);
        Assert.Equal("国网辽宁经研院2026年辽宁电网高压嵌入式直流适应性研究科技项目", package8.PackageName);

        var package9 = Assert.Single(records.Where(x =>
            x.SupplierName == "中国科学院沈阳计算技术研究所有限公司" &&
            BidOpsPackageNoNormalizer.Normalize(x.PackageNo) == "9"));
        Assert.Equal("22FK09-9012008-T035", package9.LotNo);
        Assert.Contains(records, x =>
            x.SupplierName == "东北大学" &&
            BidOpsPackageNoNormalizer.Normalize(x.PackageNo) == "12" &&
            x.LotName == "科技项目-经研院科技科研");
        Assert.DoesNotContain(records, x =>
            x.SupplierName == "东北电力大学" &&
            BidOpsPackageNoNormalizer.Normalize(x.PackageNo) == "8" &&
            string.IsNullOrWhiteSpace(x.LotNo));
    }

    [Fact]
    public void BidOpsAwardEvidenceParser_DoesNotStripChineseNumberProvincePrefix()
    {
        Assert.Equal("四川利安易昂科技有限公司", BidOpsSupplierNameNormalizer.Clean("四川利安易昂科技有限公司"));

        var records = BidOpsAwardEvidenceParser.Extract([Doc("""
项目编号：282602
073 金属检测仪器 包 2 四川利安易昂科技有限公司
""")]);

        var record = Assert.Single(records);
        Assert.Equal("2", record.NormalizedPackageNo);
        Assert.Equal("四川利安易昂科技有限公司", record.AwardedSupplierName);
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
    public void BidOpsTenderPackageEvidenceParser_ExtractsGuidePrice()
    {
        var records = BidOpsTenderPackageEvidenceParser.Extract([Doc("""
项目编号：P-007
| 分标编号 | 包号 | 包名称 | 指导价 | 最高限价 |
| --- | --- | --- | --- | --- |
| LOT-C | 包1 | 运维服务 | 100万元 | 120万元 |
""", "P-007采购公告", "ProcurementAnnouncement")]);

        var record = Assert.Single(records);
        Assert.Equal(1000000m, record.GuidePrice);
        Assert.Equal(1200000m, record.MaxPrice);
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
        Assert.Equal(BidOpsAmountKinds.CandidateFinalQuote, closure.PricingDecision?.AmountKind);
        Assert.Contains("Awarded supplier matched candidate rank 1", closure.MatchReasons);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_UsesDirectAwardAmountBeforeCandidateQuote()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: 800000m);
        var candidate = Candidate("包1", "北京甲科技有限公司", rank: 1, amount: 860000m);

        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug([award], [candidate], []));

        Assert.Equal(800000m, closure.FinalAwardAmount);
        Assert.Equal(BidOpsAmountKinds.DirectAwardAmount, closure.FinalAwardAmountSource);
        Assert.Equal(BidOpsAmountKinds.DirectAwardAmount, closure.PricingDecision?.AmountKind);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_DefaultsAwardAmountFromProcurementAmountWhenAwardAmountMissing()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: null);
        var tender = Tender("包1", budget: 1000000m);

        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug([award], [], [tender]));

        Assert.Equal(1000000m, closure.FinalAwardAmount);
        Assert.Equal(BidOpsAmountKinds.DefaultedFromProcurementPackageAmount, closure.FinalAwardAmountSource);
        Assert.Equal(BidOpsAmountKinds.DefaultedFromProcurementPackageAmount, closure.PricingDecision?.AmountKind);
        Assert.True(closure.RequiresManualReview);
        Assert.DoesNotContain("award amount missing", closure.MissingFields);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_InfersAmountFromDiscountRateAndPackageGuidePrice()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: null) with
        {
            AmountSource = BidOpsRateTypes.DiscountRate,
            RateEvidence = new BidOpsRateEvidence(
                BidOpsRateTypes.DiscountRate,
                0.9m,
                "折扣率90%",
                Source("AwardNotice"),
                0.9)
        };
        var tender = Tender("包1", budget: null) with
        {
            GuidePrice = 1000000m
        };

        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug([award], [], [tender]));

        Assert.Equal(900000m, closure.FinalAwardAmount);
        Assert.Equal(BidOpsAmountKinds.InferredFromDiscountRate, closure.FinalAwardAmountSource);
        Assert.Equal("1000000 * 0.9", closure.PricingDecision?.Formula);
        Assert.True(closure.RequiresManualReview);
    }

    [Fact]
    public void BidOpsPricingInferenceService_DoesNotInferWhenBaseAmountIsAmbiguous()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: null) with
        {
            RateEvidence = new BidOpsRateEvidence(
                BidOpsRateTypes.DiscountRate,
                0.9m,
                "折扣率90%",
                Source("AwardNotice"),
                0.9)
        };
        var tender = Tender("包1", budget: 1000000m) with
        {
            MaxPrice = 1200000m
        };

        var decision = BidOpsPricingInferenceService.Infer(award, null, [], tender);

        Assert.Null(decision.AmountValue);
        Assert.Equal(BidOpsAmountKinds.Unknown, decision.AmountKind);
        Assert.Contains("BaseAmountMissing", decision.MissingReasons);
    }

    [Fact]
    public void BidOpsPricingInferenceService_DoesNotInferUnknownPercentSemantics()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: null) with
        {
            RateEvidence = new BidOpsRateEvidence(
                BidOpsRateTypes.Unknown,
                0.9m,
                "比例90%",
                Source("AwardNotice"),
                0.6)
        };
        var tender = Tender("包1", budget: null) with
        {
            GuidePrice = 1000000m
        };

        var decision = BidOpsPricingInferenceService.Infer(award, null, [], tender);

        Assert.Null(decision.AmountValue);
        Assert.Equal(BidOpsAmountKinds.Unknown, decision.AmountKind);
        Assert.Contains("RateSemanticsAmbiguous", decision.MissingReasons);
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
    public void BidOpsReverseLifecycleClosureService_DeduplicatesLifecycleLinkDraftsByPersistenceHash()
    {
        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug(
            [Award("包1", "北京甲科技有限公司", amount: null)],
            [Candidate("包1", "北京甲科技有限公司", rank: 1, amount: 860000m)],
            [Tender("包1", budget: 1000000m)]));
        var weakerDuplicate = closure with
        {
            LinkConfidence = 0.5d,
            RequiresManualReview = true,
            FinalAwardAmount = null,
            MissingFields = ["manual review required"]
        };
        var differentSupplier = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug(
            [Award("包1", "上海乙工程有限公司", amount: 900000m)],
            [Candidate("包1", "上海乙工程有限公司", rank: 1, amount: 900000m)],
            [Tender("包1", budget: 1000000m)]));

        var drafts = BidOpsReverseLifecycleClosureService.DeduplicateLifecycleClosuresForDebug(
            300001,
            [weakerDuplicate, closure, differentSupplier]);

        Assert.Equal(2, drafts.Count);
        Assert.Contains(drafts, x => x.Award.AwardedSupplierName == "北京甲科技有限公司" && x.FinalAwardAmount == 860000m);
        Assert.Contains(drafts, x => x.Award.AwardedSupplierName == "上海乙工程有限公司");
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_TreatsConfirmedSamePackageSupplierAsEquivalentAcrossHashChanges()
    {
        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug(
            [Award("包1", "北京甲科技有限公司", amount: 860000m)],
            [],
            []));
        var confirmed = new LifecyclePackageLink
        {
            AwardRawNoticeId = 1,
            ProjectCode = "code:P-008）",
            PackageNo = "包01",
            SupplierName = "北京甲科技有限公司",
            LinkStatus = BidOpsLifecycleLinkStatuses.Confirmed
        };
        var method = typeof(BidOpsReverseLifecycleClosureService).GetMethod(
            "IsConfirmedEquivalentLifecycleLink",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.True((bool)method!.Invoke(null, new object[] { confirmed, closure })!);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_BuildsOutcomeAwardEvidenceWithReviewPackageLotContext()
    {
        var raw = new RawNotice
        {
            Id = 328339628681728000,
            SourceNoticeId = "code:282602",
            Title = "国网青海省电力公司2026年第二次（282602）物资招标采购项目中标人名单",
            NoticeType = "AwardAnnouncement",
            DetailUrl = "https://example.test/award",
            FetchTime = new DateTime(2026, 6, 27)
        };
        var record = new OutcomeSupplierRecord
        {
            RawNoticeId = raw.Id,
            ProjectCode = "282602",
            LotName = "干式电磁CT",
            PackageNo = "包 1",
            SupplierName = "广东四会互感器厂有限公司",
            ExtractionConfidence = 0.9m
        };
        var package = new PackageStaging
        {
            NoticeStagingId = 1,
            LotNo = "005",
            LotName = "干式电磁 CT",
            PackageNo = "包 1",
            PackageName = "国网青海省电力公司2026年第二次（282602）物资招标采购项目中标人名单"
        };

        var award = Assert.Single(BidOpsReverseLifecycleClosureService.BuildOutcomeAwardEvidenceForDebug(raw, [record], [package]));
        var closure = Assert.Single(BidOpsReverseLifecycleClosureService.LinkEvidenceForDebug([award], [], []));

        Assert.Equal("005", award.LotNo);
        Assert.Equal("干式电磁CT", award.LotName);
        Assert.Equal("1", award.NormalizedPackageNo);
        Assert.Equal("005", closure.LotNo);
        Assert.Equal("干式电磁CT", closure.LotName);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_EnrichesLifecycleLinkLotContextByEvidenceText()
    {
        var link = new LifecyclePackageLinkDto
        {
            AwardRawNoticeId = 328339628681728000,
            ProjectCode = "282602",
            PackageNo = "包 1",
            SupplierName = "电南瑞南京控制系统有限公司",
            EvidenceJson = """
            {
              "award": {
                "evidence": {
                  "evidenceText": "003 移动变电站 包 1 国电南瑞南京控制系统有限公司"
                }
              }
            }
            """
        };
        var unrelatedSameSupplierAndPackage = new OutcomeSupplierRecord
        {
            Id = 1,
            RawNoticeId = link.AwardRawNoticeId.Value,
            LotName = "电能质量监测及在线监测",
            PackageNo = "包 1",
            SupplierName = "国电南瑞南京控制系统有限公司",
            EvidenceText = "023 电能质量监测及在线监测 包 1 国电南瑞南京控制系统有限公司"
        };
        var expected = new OutcomeSupplierRecord
        {
            Id = 2,
            RawNoticeId = link.AwardRawNoticeId.Value,
            LotName = "移动变电站",
            PackageNo = "包 1",
            SupplierName = "国电南瑞南京控制系统有限公司",
            EvidenceText = "003 移动变电站 包 1 国电南瑞南京控制系统有限公司"
        };
        var package = new PackageStaging
        {
            LotNo = "003",
            LotName = "移动变电站",
            PackageNo = "包 1",
            PackageName = "国网青海省电力公司2026年第二次（282602）物资招标采购项目中标人名单"
        };

        BidOpsReverseLifecycleClosureService.EnrichLifecycleLinkFromOutcomeContextForDebug(
            link,
            [unrelatedSameSupplierAndPackage, expected],
            [package]);

        Assert.Equal("003", link.LotNo);
        Assert.Equal("移动变电站", link.LotName);
        Assert.Equal("包 1", link.PackageNo);
        Assert.Equal("国电南瑞南京控制系统有限公司", link.SupplierName);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_EnrichesGenericAwardLotFromProcurementPackage()
    {
        var link = new LifecyclePackageLinkDto
        {
            AwardRawNoticeId = 327333277306327040,
            ProcurementRawNoticeId = 327854700416339968,
            ProjectCode = "22FK09）",
            LotName = "未分标段",
            PackageNo = "包 8",
            SupplierName = "东北电力大学"
        };
        var record = new OutcomeSupplierRecord
        {
            Id = 1,
            RawNoticeId = link.AwardRawNoticeId.Value,
            ProjectCode = "22FK09）",
            LotName = "未分标段",
            PackageNo = "包 8",
            SupplierName = "东北电力大学",
            EvidenceText = "东北电力大学"
        };
        var procurementPackage = new PackageStaging
        {
            LotNo = "22FK09-9012008-T035",
            LotName = "科技项目-经研院科技科研",
            PackageNo = "8",
            PackageName = "国网辽宁经研院2026年辽宁电网高压嵌入式直流适应性研究科技项目"
        };

        BidOpsReverseLifecycleClosureService.EnrichLifecycleLinkFromOutcomeContextForDebug(
            link,
            [record],
            [procurementPackage]);

        Assert.Equal("22FK09", link.ProjectCode);
        Assert.Equal("22FK09-9012008-T035", link.LotNo);
        Assert.Equal("科技项目-经研院科技科研", link.LotName);
        Assert.Equal("国网辽宁经研院2026年辽宁电网高压嵌入式直流适应性研究科技项目", link.PackageName);
    }

    [Fact]
    public void BidOpsReverseLifecycleClosureService_EnrichesProcurementPackageAmountByLotNameAndPackageNo()
    {
        var link = new LifecyclePackageLinkDto
        {
            AwardRawNoticeId = 1,
            ProcurementRawNoticeId = 2,
            LotName = "科技项目-经研院科技科研",
            PackageNo = "包 8",
            SupplierName = "东北电力大学"
        };
        var record = new OutcomeSupplierRecord
        {
            Id = 10,
            RawNoticeId = 1,
            LotName = "科技项目-经研院科技科研",
            PackageNo = "8",
            SupplierName = "东北电力大学"
        };
        var expectedDetail = new ProcurementDetailStaging
        {
            Id = 100,
            RawNoticeId = 2,
            LotName = "科技项目-经研院科技科研",
            PackageNo = "8",
            PackageEstimatedAmount = 860000m
        };
        var samePackageOtherLot = new ProcurementDetailStaging
        {
            Id = 101,
            RawNoticeId = 2,
            LotName = "物资项目-配网材料",
            PackageNo = "8",
            PackageEstimatedAmount = 1200000m
        };

        BidOpsReverseLifecycleClosureService.EnrichLifecycleLinkFromOutcomeContextForDebug(
            link,
            [record],
            [],
            [samePackageOtherLot, expectedDetail]);

        Assert.Equal(860000m, link.ProcurementPackageAmount);
        Assert.Equal("ProcurementDetailStaging.PackageEstimatedAmount", link.ProcurementPackageAmountSource);
        Assert.Equal(100, link.ProcurementDetailStagingId);
        Assert.Equal(860000m, link.FinalAwardAmount);
        Assert.Equal(BidOpsAmountKinds.DefaultedFromProcurementPackageAmount, link.FinalAwardAmountSource);
    }

    [Fact]
    public void BidOpsNoticeCorrelationService_ScoresProjectPackageSupplierAndTimeEvidence()
    {
        var award = Award("包1", "北京甲科技有限公司", amount: null);
        var raw = new RawNotice
        {
            Id = 10,
            Title = "P-008 推荐中标候选人公示",
            NoticeType = "CandidateAnnouncement",
            DetailUrl = "https://example.test/candidate",
            SourceNoticeId = "code:P-008",
            PublishTime = new DateTime(2026, 6, 1),
            TextPreview = "项目编号：P-008 分标编号：LOT-A 包1 北京甲科技有限公司 第一候选人"
        };

        var match = BidOpsNoticeCorrelationService.Match(
            raw,
            [Doc(raw.TextPreview, raw.Title, raw.NoticeType)],
            [award],
            "Candidate",
            new DateTime(2026, 6, 2));

        Assert.Equal("High", match.ConfidenceLevel);
        Assert.True(match.Confidence >= 0.75);
        Assert.Contains("ProjectCode exact match", match.MatchReasons);
        Assert.Contains("Awarded supplier appears in candidate notice text", match.MatchReasons);
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
        var enqueueRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.EnqueueReverseCloseAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var persistRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.ReverseCloseRawNoticeAndPersistAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var linksRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.SearchLifecycleLinksAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var procurementCandidatesRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.SearchProcurementNoticeCandidatesAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var procurementImportRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.ImportProcurementNoticeCandidateAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var fieldEnrichmentRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.EnqueueFieldEnrichmentAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var outcomeSupplierReparseRoute = typeof(LifecycleDebugController)
            .GetMethod(nameof(LifecycleDebugController.EnqueueOutcomeSupplierReparseAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("api/bidops/lifecycle/debug", controllerRoute);
        Assert.Equal("links", linksRoute);
        Assert.Equal("links/{linkId:long}/procurement-candidates", procurementCandidatesRoute);
        Assert.Equal("links/{linkId:long}/procurement-candidates/import", procurementImportRoute);
        Assert.Equal("links/{linkId:long}/field-enrichment/enqueue", fieldEnrichmentRoute);
        Assert.Equal("award-notices/{rawNoticeId:long}/outcome-supplier-reparse/enqueue", outcomeSupplierReparseRoute);
        Assert.Equal("reverse-close-url", urlRoute);
        Assert.Equal("reverse-close-raw-notice/{rawNoticeId:long}", rawRoute);
        Assert.Equal("reverse-close/enqueue", enqueueRoute);
        Assert.Equal("reverse-close-raw-notice/{rawNoticeId:long}/persist", persistRoute);
    }

    [Fact]
    public void LifecycleReverseClosureDeduplicationKey_IncludesRunIdForManualAnalysis()
    {
        var type = typeof(LifecycleDebugController).Assembly
            .GetType("Atlas.Modules.BidOps.BidOpsBackgroundJobDeduplicationKeys");
        var method = type?.GetMethod(
            "LifecycleReverseClosure",
            BindingFlags.Static | BindingFlags.Public);
        var longUrl = "https://example.test/" + new string('a', 400);

        Assert.NotNull(type);
        Assert.NotNull(method);

        var first = (string)method!.Invoke(
            null,
            [300001L, 123L, longUrl, true, "run-a"])!;
        var second = (string)method!.Invoke(
            null,
            [300001L, 123L, longUrl, true, "run-b"])!;

        Assert.NotEqual(first, second);
        Assert.EndsWith(":run:run-a", first);
        Assert.EndsWith(":run:run-b", second);
        Assert.True(first.Length <= 300);
        Assert.True(second.Length <= 300);
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
