using System.IO.Compression;
using System.Net;
using System.Reflection;
using System.Text;
using System.Text.Json;
using Atlas.BackgroundTasks;
using Atlas.Core.Authorization;
using Atlas.Core.Entities.Global;
using Atlas.Core.Services;
using Atlas.Extensions.DependencyInjection;
using Atlas.Modules.BidOps;
using Atlas.Modules.BidOps.Ai;
using Atlas.Modules.BidOps.Ai.Evidence;
using Atlas.Modules.BidOps.BackgroundJobs;
using Atlas.Modules.BidOps.Controllers;
using Atlas.Modules.BidOps.Crawling;
using Atlas.Modules.BidOps.Documents;
using Atlas.Modules.BidOps.EntityConfigurations;
using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Tendering;
using Atlas.Modules.BidOps.Models;
using Atlas.Modules.BidOps.Queries;
using Atlas.Modules.BidOps.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
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
    public void BidOpsBusinessNumberBuilder_UsesFullIdToAvoidLowSixDigitCollisions()
    {
        var timestamp = new DateTime(2026, 6, 22, 1, 2, 3, DateTimeKind.Utc);
        var first = BidOpsBusinessNumberBuilder.Build("SUP", 327344015848640512, timestamp);
        var second = BidOpsBusinessNumberBuilder.Build("SUP", 327344015849640512, timestamp);

        Assert.StartsWith("SUP-20260622-", first);
        Assert.StartsWith("SUP-20260622-", second);
        Assert.NotEqual(first, second);
        Assert.False(first.EndsWith("-640512", StringComparison.Ordinal));
    }

    [Fact]
    public void RawNoticeConfiguration_UsesBusinessIdentityUniqueIndex()
    {
        var modelBuilder = new ModelBuilder();
        new RawNoticeConfiguration().Configure(modelBuilder.Entity<RawNotice>());

        var entityType = modelBuilder.Model.FindEntityType(typeof(RawNotice));
        Assert.NotNull(entityType);

        Assert.Contains(entityType!.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(RawNotice.TenantId),
                nameof(RawNotice.NoticeType),
                nameof(RawNotice.SourceNoticeId)
            ]));
    }

    [Fact]
    public void BidOpsRuntimeSettingConfiguration_UsesTenantScopedUniqueKey()
    {
        var modelBuilder = new ModelBuilder();
        new BidOpsRuntimeSettingConfiguration().Configure(modelBuilder.Entity<BidOpsRuntimeSetting>());

        var entityType = modelBuilder.Model.FindEntityType(typeof(BidOpsRuntimeSetting));
        Assert.NotNull(entityType);

        Assert.Equal("bidops_runtime_setting", entityType!.GetTableName());
        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(BidOpsRuntimeSetting.TenantId),
                nameof(BidOpsRuntimeSetting.SettingKey)
            ]));
    }

    [Fact]
    public void ProcurementDetailConfiguration_MapsCoreIndexesAndJsonColumns()
    {
        var modelBuilder = new ModelBuilder();
        new ProcurementDetailStagingConfiguration().Configure(modelBuilder.Entity<ProcurementDetailStaging>());
        new ProcurementDetailConfiguration().Configure(modelBuilder.Entity<ProcurementDetail>());

        var stagingType = modelBuilder.Model.FindEntityType(typeof(ProcurementDetailStaging));
        var formalType = modelBuilder.Model.FindEntityType(typeof(ProcurementDetail));
        Assert.NotNull(stagingType);
        Assert.NotNull(formalType);

        Assert.Equal("bidops_procurement_detail_staging", stagingType!.GetTableName());
        Assert.Equal("bidops_procurement_detail", formalType!.GetTableName());
        Assert.Equal("longtext", formalType.FindProperty(nameof(ProcurementDetail.OriginalRowJson))!.GetColumnType());
        Assert.Equal("longtext", formalType.FindProperty(nameof(ProcurementDetail.NormalizedFieldsJson))!.GetColumnType());
        Assert.Equal("decimal(18,2)", formalType.FindProperty(nameof(ProcurementDetail.MaxPrice))!.GetColumnType());
        Assert.Equal("decimal(5,2)", formalType.FindProperty(nameof(ProcurementDetail.BusinessWeight))!.GetColumnType());

        Assert.Contains(formalType.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(ProcurementDetail.TenantId),
                nameof(ProcurementDetail.ProjectCode),
                nameof(ProcurementDetail.LotNo),
                nameof(ProcurementDetail.PackageNo)
            ]));
        Assert.Contains(stagingType.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(ProcurementDetailStaging.TenantId),
                nameof(ProcurementDetailStaging.RawNoticeId),
                nameof(ProcurementDetailStaging.RawAttachmentId),
                nameof(ProcurementDetailStaging.TableIndex),
                nameof(ProcurementDetailStaging.RowIndex)
            ]));
    }

    [Fact]
    public void LifecyclePackageLinkConfiguration_UsesTenantScopedMatchIndex()
    {
        var modelBuilder = new ModelBuilder();
        new LifecyclePackageLinkConfiguration().Configure(modelBuilder.Entity<LifecyclePackageLink>());

        var entityType = modelBuilder.Model.FindEntityType(typeof(LifecyclePackageLink));
        Assert.NotNull(entityType);

        Assert.Equal("bidops_lifecycle_package_link", entityType!.GetTableName());
        Assert.Equal("decimal(18,2)", entityType.FindProperty(nameof(LifecyclePackageLink.FinalAwardAmount))!.GetColumnType());
        Assert.Equal("decimal(5,4)", entityType.FindProperty(nameof(LifecyclePackageLink.MatchScore))!.GetColumnType());
        Assert.Equal("longtext", entityType.FindProperty(nameof(LifecyclePackageLink.EvidenceJson))!.GetColumnType());
        Assert.Contains(entityType.GetIndexes(), index =>
            index.IsUnique &&
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(LifecyclePackageLink.TenantId),
                nameof(LifecyclePackageLink.SourceHash)
            ]));
        Assert.Contains(entityType.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(LifecyclePackageLink.TenantId),
                nameof(LifecyclePackageLink.LinkStatus),
                nameof(LifecyclePackageLink.RequiresManualReview),
                nameof(LifecyclePackageLink.MatchScore)
            ]));
    }

    [Fact]
    public void ReviewQualityConfiguration_MapsTaskSummaryAndIssueIndexes()
    {
        var modelBuilder = new ModelBuilder();
        new ReviewTaskConfiguration().Configure(modelBuilder.Entity<ReviewTask>());
        new ReviewQualityIssueConfiguration().Configure(modelBuilder.Entity<ReviewQualityIssue>());
        new ReviewCorrectionSampleConfiguration().Configure(modelBuilder.Entity<ReviewCorrectionSample>());

        var taskType = modelBuilder.Model.FindEntityType(typeof(ReviewTask));
        var issueType = modelBuilder.Model.FindEntityType(typeof(ReviewQualityIssue));
        var correctionType = modelBuilder.Model.FindEntityType(typeof(ReviewCorrectionSample));
        Assert.NotNull(taskType);
        Assert.NotNull(issueType);
        Assert.NotNull(correctionType);

        Assert.Equal("bidops_review_task", taskType!.GetTableName());
        Assert.NotNull(taskType.FindProperty(nameof(ReviewTask.QualityScore)));
        Assert.NotNull(taskType.FindProperty(nameof(ReviewTask.RiskLevel)));
        Assert.NotNull(taskType.FindProperty(nameof(ReviewTask.ReviewRecommendation)));
        Assert.Contains(taskType.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(ReviewTask.TenantId),
                nameof(ReviewTask.RiskLevel),
                nameof(ReviewTask.Status),
                nameof(ReviewTask.CreatedAt)
            ]));

        Assert.Equal("bidops_review_quality_issue", issueType!.GetTableName());
        Assert.Equal("longtext", issueType.FindProperty(nameof(ReviewQualityIssue.EvidenceJson))!.GetColumnType());
        Assert.Contains(issueType.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(ReviewQualityIssue.TenantId),
                nameof(ReviewQualityIssue.ReviewTaskId),
                nameof(ReviewQualityIssue.IsResolved)
            ]));
        Assert.Contains(issueType.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(ReviewQualityIssue.TenantId),
                nameof(ReviewQualityIssue.Severity),
                nameof(ReviewQualityIssue.CreatedAt)
            ]));

        Assert.Equal("bidops_review_correction_sample", correctionType!.GetTableName());
        Assert.Equal("longtext", correctionType.FindProperty(nameof(ReviewCorrectionSample.ReviewerPrompt))!.GetColumnType());
        Assert.Contains(correctionType.GetIndexes(), index =>
            index.Properties.Select(x => x.Name).SequenceEqual([
                nameof(ReviewCorrectionSample.TenantId),
                nameof(ReviewCorrectionSample.SourceKind),
                nameof(ReviewCorrectionSample.CreatedAt)
            ]));
    }

    [Fact]
    public void BidOpsReviewQualityEvaluator_CompleteProcurementNoticeIsLowRisk()
    {
        var notice = new NoticeStaging
        {
            Id = 1,
            NoticeType = "ProcurementAnnouncement",
            ProjectCode = "19FBAC",
            ProjectName = "国网测试采购项目"
        };
        var package = new PackageStaging
        {
            Id = 11,
            NoticeStagingId = notice.Id,
            LotNo = "19FBAC-9012002-9999",
            LotName = "技术服务",
            PackageNo = "包1",
            PackageName = "包1技术服务",
            MaxPrice = 457800m
        };
        var requirements = new[]
        {
            new RequirementStaging { PackageStagingId = package.Id, RequirementType = "Qualification", OriginalText = "具有工程咨询资信证书" },
            new RequirementStaging { PackageStagingId = package.Id, RequirementType = "Performance", OriginalText = "近三年具有类似业绩" },
            new RequirementStaging { PackageStagingId = package.Id, RequirementType = "Personnel", OriginalText = "项目负责人具备中级职称" }
        };
        var detail = new ProcurementDetailStaging
        {
            Id = 21,
            NoticeStagingId = notice.Id,
            PackageStagingId = package.Id,
            SourceSheetName = "采购一览表",
            OriginalRowJson = "{\"最高限价(万元)\":\"45.78\"}",
            MaxPrice = 457800m
        };

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateNotice(
            notice,
            [package],
            requirements,
            [detail]);

        Assert.Equal(ReviewQualityRiskLevel.Low, evaluation.RiskLevel);
        Assert.Equal(ReviewRecommendation.BatchConfirmCandidate, evaluation.ReviewRecommendation);
        Assert.Equal(100, evaluation.QualityScore);
        Assert.Empty(evaluation.Issues);
    }

    [Fact]
    public void BidOpsReviewQualityEvaluator_FlagsProcurementQualityIssues()
    {
        var notice = new NoticeStaging
        {
            Id = 1,
            NoticeType = "ProcurementAnnouncement",
            ProjectCode = "19FBAC",
            ProjectName = "国网测试采购项目"
        };
        var package = new PackageStaging
        {
            Id = 11,
            NoticeStagingId = notice.Id,
            LotNo = "19FBAC-9012002-9999",
            LotName = "技术服务",
            PackageNo = "包1",
            PackageName = "包1技术服务",
            MaxPrice = 45.78m
        };
        var tenThousandDetail = new ProcurementDetailStaging
        {
            Id = 21,
            NoticeStagingId = notice.Id,
            PackageStagingId = package.Id,
            SourceSheetName = "采购一览表",
            OriginalRowJson = "{\"最高应答限价含税（万元）\":\"45.78\"}",
            MaxPrice = 45.78m
        };
        var percentDetail = new ProcurementDetailStaging
        {
            Id = 22,
            NoticeStagingId = notice.Id,
            PackageStagingId = package.Id,
            SourceSheetName = "采购一览表",
            OriginalRowJson = "{\"最高限价（%）\":\"97.5\"}",
            MaxPrice = 97.5m
        };

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateNotice(
            notice,
            [package],
            [],
            [tenThousandDetail, percentDetail]);

        Assert.Equal(ReviewQualityRiskLevel.High, evaluation.RiskLevel);
        Assert.Equal(ReviewRecommendation.NeedsReparse, evaluation.ReviewRecommendation);
        Assert.Contains(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.AmbiguousAmountUnit &&
            x.Severity == ReviewQualityRiskLevel.High);
        Assert.Contains(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.RateOrDiscountInAmountColumn &&
            x.Severity == ReviewQualityRiskLevel.High);
        Assert.Contains(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.MissingQualificationRequirement &&
            x.Severity == ReviewQualityRiskLevel.Medium);
    }

    [Fact]
    public void BidOpsReviewQualityEvaluator_FlagsMissingPackageIdentity()
    {
        var notice = new NoticeStaging
        {
            Id = 1,
            NoticeType = "TenderAnnouncement",
            ProjectCode = "872610",
            ProjectName = "国网测试采购项目"
        };
        var package = new PackageStaging
        {
            Id = 11,
            NoticeStagingId = notice.Id,
            LotNo = "872610-9012002",
            LotName = "技术服务",
            PackageNo = "",
            PackageName = "包1技术服务"
        };
        var requirements = new[]
        {
            new RequirementStaging { PackageStagingId = package.Id, RequirementType = "Qualification", OriginalText = "具备有效资质证书" },
            new RequirementStaging { PackageStagingId = package.Id, RequirementType = "Performance", OriginalText = "具有类似业绩" },
            new RequirementStaging { PackageStagingId = package.Id, RequirementType = "Personnel", OriginalText = "项目负责人满足要求" }
        };

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateNotice(notice, [package], requirements);

        Assert.Equal(ReviewQualityRiskLevel.Medium, evaluation.RiskLevel);
        Assert.Contains(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.MissingLotOrPackage &&
            x.FieldName == nameof(PackageStaging.PackageNo) &&
            x.Severity == ReviewQualityRiskLevel.Medium);
    }

    [Fact]
    public void BidOpsReviewQualityEvaluator_CompleteOutcomeNoticeIsLowRisk()
    {
        var raw = new RawNotice
        {
            Id = 1,
            TenantId = 300001,
            NoticeType = "AwardAnnouncement",
            Title = "国网测试项目中标结果公告"
        };
        var notice = new NoticeStaging
        {
            Id = 2,
            RawNoticeId = raw.Id,
            NoticeType = "AwardAnnouncement",
            ProjectName = "国网测试项目",
            ProjectCode = "872610"
        };
        var package = new PackageStaging
        {
            Id = 3,
            NoticeStagingId = notice.Id,
            LotNo = "872610-9012002",
            PackageNo = "包1",
            PackageName = "包1技术服务"
        };
        var record = new OutcomeSupplierRecord
        {
            Id = 4,
            RawNoticeId = raw.Id,
            NoticeType = "AwardAnnouncement",
            LotNo = package.LotNo,
            PackageNo = package.PackageNo,
            SupplierName = "北京测试科技有限公司",
            OutcomeType = BidOpsOutcomeTypes.Awarded,
            AwardAmount = 457800m,
            EvidenceText = "包1 中标人 北京测试科技有限公司 中标金额45.78万元"
        };

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateOutcomeNotice(raw, notice, [record], [package]);

        Assert.Equal(ReviewQualityRiskLevel.Low, evaluation.RiskLevel);
        Assert.Equal(ReviewRecommendation.BatchConfirmCandidate, evaluation.ReviewRecommendation);
        Assert.Empty(evaluation.Issues);
    }

    [Fact]
    public void BidOpsReviewQualityEvaluator_MatchesOutcomePackageByLotNameAndPackageNo()
    {
        var raw = new RawNotice
        {
            Id = 1,
            TenantId = 300001,
            NoticeType = "AwardAnnouncement",
            Title = "国网测试项目成交结果公告"
        };
        var notice = new NoticeStaging
        {
            Id = 2,
            RawNoticeId = raw.Id,
            NoticeType = "AwardAnnouncement",
            ProjectName = "国网测试项目",
            ProjectCode = "872610"
        };
        var packages = new[]
        {
            new PackageStaging
            {
                Id = 10,
                NoticeStagingId = notice.Id,
                LotNo = "872610-9001005",
                LotName = "综合服务",
                PackageNo = "包1",
                PackageName = "包1综合服务"
            },
            new PackageStaging
            {
                Id = 11,
                NoticeStagingId = notice.Id,
                LotNo = "872610-9001005",
                LotName = "运维服务",
                PackageNo = "包1",
                PackageName = "包1运维服务"
            }
        };
        var record = new OutcomeSupplierRecord
        {
            Id = 4,
            RawNoticeId = raw.Id,
            NoticeType = "AwardAnnouncement",
            LotNo = "872610-9001005",
            LotName = "综合服务",
            PackageNo = "包1",
            SupplierName = "北京测试科技有限公司",
            OutcomeType = BidOpsOutcomeTypes.Awarded,
            AwardAmount = 100000m,
            EvidenceText = "综合服务 包1 成交人 北京测试科技有限公司 成交金额10万元"
        };

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateOutcomeNotice(raw, notice, [record], packages);

        Assert.Equal(ReviewQualityRiskLevel.Low, evaluation.RiskLevel);
        Assert.DoesNotContain(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.LifecycleMatchConflict ||
            x.IssueType == BidOpsReviewQualityIssueTypes.LifecycleMatchMissing);
    }

    [Fact]
    public void BidOpsReviewQualityEvaluator_MatchesOutcomePackageByLotNoAndPackageNoWhenPackageNoRepeats()
    {
        var raw = new RawNotice
        {
            Id = 1,
            TenantId = 300001,
            NoticeType = "AwardAnnouncement",
            Title = "国网测试项目中标结果公告"
        };
        var notice = new NoticeStaging
        {
            Id = 2,
            RawNoticeId = raw.Id,
            NoticeType = "AwardAnnouncement",
            ProjectName = "国网测试项目",
            ProjectCode = "122609"
        };
        var packages = new[]
        {
            new PackageStaging
            {
                Id = 10,
                NoticeStagingId = notice.Id,
                LotNo = "122609-1005000-9999",
                LotName = "未分标段",
                PackageNo = "包 1"
            },
            new PackageStaging
            {
                Id = 11,
                NoticeStagingId = notice.Id,
                LotNo = "122609-9201000-0003",
                LotName = "未分标段",
                PackageNo = "包 1"
            }
        };
        var record = new OutcomeSupplierRecord
        {
            Id = 12,
            RawNoticeId = raw.Id,
            NoticeType = "AwardAnnouncement",
            LotNo = "122609-9201000-0003",
            LotName = "未分标段",
            PackageNo = "包 1",
            SupplierName = "河南平高电气股份有限公司",
            OutcomeType = BidOpsOutcomeTypes.Awarded,
            EvidenceText = "122609-9201000-0003 包 1 河南平高电气股份有限公司"
        };

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateOutcomeNotice(raw, notice, [record], packages);

        Assert.Equal(ReviewQualityRiskLevel.Low, evaluation.RiskLevel);
        Assert.DoesNotContain(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.LifecycleMatchConflict ||
            x.IssueType == BidOpsReviewQualityIssueTypes.LifecycleMatchMissing);
    }

    [Fact]
    public void BidOpsReviewQualityEvaluator_DoesNotTreatDifferentLotNamesAsDuplicatePackageIdentity()
    {
        var notice = new NoticeStaging
        {
            Id = 2,
            RawNoticeId = 1,
            NoticeType = "ProcurementAnnouncement",
            ProjectName = "国网测试项目",
            ProjectCode = "872610"
        };
        var packages = new[]
        {
            new PackageStaging
            {
                Id = 10,
                NoticeStagingId = notice.Id,
                LotNo = "872610-9001005",
                LotName = "综合服务",
                PackageNo = "包1",
                PackageName = "包1综合服务"
            },
            new PackageStaging
            {
                Id = 11,
                NoticeStagingId = notice.Id,
                LotNo = "872610-9001005",
                LotName = "运维服务",
                PackageNo = "包1",
                PackageName = "包1运维服务"
            }
        };

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateNotice(notice, packages, []);

        Assert.DoesNotContain(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.DuplicatePackageIdentity);
    }

    [Fact]
    public void BidOpsReviewQualityEvaluator_FlagsOutcomeQualityIssues()
    {
        var raw = new RawNotice
        {
            Id = 1,
            TenantId = 300001,
            NoticeType = "CandidateAnnouncement",
            Title = "国网测试项目中标候选人公示"
        };
        var notice = new NoticeStaging
        {
            Id = 2,
            RawNoticeId = raw.Id,
            NoticeType = "CandidateAnnouncement",
            ProjectName = "国网测试项目"
        };
        var packages = new[]
        {
            new PackageStaging { Id = 10, NoticeStagingId = notice.Id, LotNo = "A", PackageNo = "包1", PackageName = "包1" },
            new PackageStaging { Id = 11, NoticeStagingId = notice.Id, LotNo = "B", PackageNo = "包1", PackageName = "包1" }
        };
        var record = new OutcomeSupplierRecord
        {
            Id = 4,
            RawNoticeId = raw.Id,
            NoticeType = "CandidateAnnouncement",
            PackageNo = "包1",
            SupplierName = "北京测试科技有限公司",
            OutcomeType = BidOpsOutcomeTypes.Candidate,
            AwardAmount = 95m,
            EvidenceText = "包1 第一候选人 北京测试科技有限公司 折扣率95%"
        };

        var evaluation = BidOpsReviewQualityEvaluator.EvaluateOutcomeNotice(raw, notice, [record], packages);

        Assert.Equal(ReviewQualityRiskLevel.High, evaluation.RiskLevel);
        Assert.Contains(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.RateOrDiscountInAmountColumn &&
            x.Severity == ReviewQualityRiskLevel.High);
        Assert.Contains(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.MissingCandidateRank &&
            x.Severity == ReviewQualityRiskLevel.Medium);
        Assert.Contains(evaluation.Issues, x =>
            x.IssueType == BidOpsReviewQualityIssueTypes.LifecycleMatchConflict &&
            x.Severity == ReviewQualityRiskLevel.High);
    }

    [Fact]
    public void BidOpsQueryService_MapsProcurementDetailDtosWithRawJsonAndAmounts()
    {
        var staging = new ProcurementDetailStaging
        {
            Id = 11,
            NoticeStagingId = 22,
            PackageStagingId = 33,
            RawNoticeId = 44,
            RawAttachmentId = 55,
            TableIndex = 2,
            RowIndex = 9,
            SourceSheetName = "采购一览表",
            ProjectCode = "19FBAC",
            LotNo = "19FBAC-9013001-3000",
            LotName = "房屋维修-施工",
            PackageNo = "包1",
            PackageName = "包1房屋维修施工",
            ProcurementContent = "房屋维修施工服务",
            ProjectOverview = "完成配套施工。",
            MaxPrice = 457800m,
            QualificationRequirement = "具有建筑工程施工总承包资质",
            PerformanceRequirement = "近三年类似业绩",
            PersonnelRequirement = "项目负责人具备证书",
            OriginalRowJson = "{\"最高限价(万元)\":\"45.78\"}",
            NormalizedFieldsJson = "{\"maxPrice\":457800}",
            AiConfidence = 0.96m,
            ReviewStatus = ReviewStatus.Pending
        };
        var formal = new ProcurementDetail
        {
            Id = 101,
            NoticeId = 202,
            TenderPackageId = 303,
            ProcurementDetailStagingId = staging.Id,
            RawNoticeId = staging.RawNoticeId,
            SourceSheetName = staging.SourceSheetName,
            ProjectCode = staging.ProjectCode,
            LotNo = staging.LotNo,
            PackageNo = staging.PackageNo,
            PackageName = staging.PackageName,
            ProcurementContent = staging.ProcurementContent,
            MaxPrice = staging.MaxPrice,
            OriginalRowJson = staging.OriginalRowJson,
            NormalizedFieldsJson = staging.NormalizedFieldsJson,
            Status = "Active"
        };

        var stagingDto = InvokePrivateMapper<ProcurementDetailStagingDto>(
            "MapProcurementDetailStaging",
            staging);
        var formalDto = InvokePrivateMapper<ProcurementDetailDto>(
            "MapProcurementDetail",
            formal);

        Assert.Equal("19FBAC-9013001-3000", stagingDto.LotNo);
        Assert.Equal("包1房屋维修施工", stagingDto.PackageName);
        Assert.Equal(457800m, stagingDto.MaxPrice);
        Assert.Contains("45.78", stagingDto.OriginalRowJson, StringComparison.Ordinal);
        Assert.Contains("建筑工程施工总承包资质", stagingDto.QualificationRequirement, StringComparison.Ordinal);
        Assert.Equal(303, formalDto.TenderPackageId);
        Assert.Equal(457800m, formalDto.MaxPrice);
        Assert.Equal("{\"maxPrice\":457800}", formalDto.NormalizedFieldsJson);
    }

    [Fact]
    public void BidOpsQueryService_MapsReviewQualityDtos()
    {
        var task = new ReviewTask
        {
            Id = 1,
            BizType = "NoticeStaging",
            BizId = 2,
            RawNoticeId = 3,
            TaskTitle = "审核标讯：测试项目",
            Status = ReviewTaskStatus.Pending,
            QualityScore = 55,
            RiskLevel = ReviewQualityRiskLevel.High,
            QualityIssueCount = 2,
            HighRiskIssueCount = 1,
            ReviewRecommendation = ReviewRecommendation.NeedsReparse
        };
        var issue = new ReviewQualityIssue
        {
            Id = 10,
            ReviewTaskId = task.Id,
            RawNoticeId = 3,
            NoticeStagingId = 2,
            PackageStagingId = 11,
            IssueType = BidOpsReviewQualityIssueTypes.AmbiguousAmountUnit,
            Severity = ReviewQualityRiskLevel.High,
            FieldName = nameof(PackageStaging.MaxPrice),
            Message = "金额单位异常",
            EvidenceJson = "{\"originalHeader\":\"最高限价(万元)\"}",
            CreatedAt = new DateTime(2026, 6, 22, 9, 0, 0)
        };

        var taskDto = InvokePrivateMapper<ReviewTaskDto>("Map", task);
        var issueDto = InvokePrivateMapper<ReviewQualityIssueDto>("MapReviewQualityIssue", issue);

        Assert.Equal(55, taskDto.QualityScore);
        Assert.Equal("High", taskDto.RiskLevel);
        Assert.Equal(2, taskDto.QualityIssueCount);
        Assert.Equal(1, taskDto.HighRiskIssueCount);
        Assert.Equal("NeedsReparse", taskDto.ReviewRecommendation);
        Assert.Equal("High", issueDto.Severity);
        Assert.Equal(BidOpsReviewQualityIssueTypes.AmbiguousAmountUnit, issueDto.IssueType);
        Assert.Contains("最高限价", issueDto.EvidenceJson, StringComparison.Ordinal);
    }

    [Fact]
    public void ReviewTaskSearchQuery_ExposesReviewQualityFilters()
    {
        var query = new ReviewTaskSearchQuery
        {
            ProjectCode = "SGCC-001",
            RiskLevel = "High",
            MinQualityScore = 40,
            MaxQualityScore = 80,
            HasHighRiskIssue = true,
            ReviewRecommendation = "NeedsReparse",
            IssueType = BidOpsReviewQualityIssueTypes.AmbiguousAmountUnit
        };

        Assert.Equal("SGCC-001", query.ProjectCode);
        Assert.Equal("High", query.RiskLevel);
        Assert.Equal(40, query.MinQualityScore);
        Assert.Equal(80, query.MaxQualityScore);
        Assert.True(query.HasHighRiskIssue);
        Assert.Equal("NeedsReparse", query.ReviewRecommendation);
        Assert.Equal(BidOpsReviewQualityIssueTypes.AmbiguousAmountUnit, query.IssueType);
    }

    private static T InvokePrivateMapper<T>(string methodName, object value)
    {
        var method = typeof(BidOpsQueryService)
            .GetMethods(BindingFlags.Static | BindingFlags.NonPublic)
            .SingleOrDefault(x =>
                x.Name == methodName &&
                x.GetParameters().Length == 1 &&
                x.GetParameters()[0].ParameterType == value.GetType());

        Assert.NotNull(method);
        return Assert.IsType<T>(method!.Invoke(null, [value]));
    }

    [Fact]
    public void BidOpsRawIngestionService_BuildsBusinessSourceNoticeIdFromProjectCode()
    {
        var method = typeof(BidOpsRawIngestionService).GetMethod(
            "BuildSourceNoticeId",
            BindingFlags.Static | BindingFlags.NonPublic);
        Assert.NotNull(method);

        var fromProjectCode = Assert.IsType<string>(method.Invoke(
            null,
            ["ProjectCode: 19FBAC", new string('a', 64)]));
        var fallback = Assert.IsType<string>(method.Invoke(
            null,
            ["没有采购编号", new string('b', 64)]));

        Assert.Equal("code:19FBAC", fromProjectCode);
        Assert.Equal($"url:{new string('b', 64)}", fallback);
    }

    [Fact]
    public void BidOpsModule_RegistersServicesAndBackgroundHandlers()
    {
        var services = new ServiceCollection();
        var module = new BidOpsModule();
        module.AddServices(new AtlasModuleContext(services, new ConfigurationBuilder().Build(), module));

        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsCrawlService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsReviewService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsReviewQualityService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOpportunityService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOpportunityMaintenanceService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsSupplierService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOutcomeSupplierExtractionService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOutcomeSupplierAiExtractionService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsAiSettingsService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsRuntimeControlService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsAiCallDiagnostics));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsCodexCliClient));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOrganizationMasterDataService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsSupplierMaintenanceService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsMatchingService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsPursuitService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsReverseLifecycleClosureService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsOperationsQueryService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsCrawlAdapter) && x.ImplementationType == typeof(StateGridEcpCrawlAdapter));
        Assert.Contains(services, x => x.ServiceType == typeof(IStateGridEcpCrawler));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsAttachmentProcessingService));
        Assert.Contains(services, x => x.ServiceType == typeof(IBidOpsTextExtractor));
        Assert.Contains(services, x => x.ServiceType == typeof(IBackgroundJobExecutionGate) && x.ImplementationType == typeof(BidOpsBackgroundJobExecutionGate));
        Assert.Contains(services, x => x.ServiceType == typeof(IBackgroundJobHandler) && x.ImplementationType == typeof(ReviewQualityBackfillJobHandler));
        Assert.True(services.Count(x => x.ServiceType == typeof(IBackgroundJobHandler)) >= 15);
        Assert.True(services.Count(x => x.ServiceType == typeof(IRecurringTask)) >= 4);
    }

    [Fact]
    public void BidOpsModule_ConfiguresAiHttpClientsWithThirtyMinuteTimeout()
    {
        var services = new ServiceCollection();
        var module = new BidOpsModule();
        module.AddServices(new AtlasModuleContext(services, new ConfigurationBuilder().Build(), module));

        using var provider = services.BuildServiceProvider();
        var factory = provider.GetRequiredService<IHttpClientFactory>();

        Assert.Equal(TimeSpan.FromMinutes(30), factory.CreateClient(nameof(IBidOpsAiExtractionService)).Timeout);
        Assert.Equal(TimeSpan.FromMinutes(30), factory.CreateClient(nameof(IBidOpsOutcomeSupplierAiExtractionService)).Timeout);
    }

    [Fact]
    public async Task BidOpsStructuredExtractionService_UsesCodexCliProvider()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            1234,
            "{\"noticeType\":\"ProcurementAnnouncement\"}",
            "progress",
            """
{
  "noticeType": "ProcurementAnnouncement",
  "projectName": "国网测试采购项目",
  "projectCode": "872610",
  "buyerName": "国网测试采购单位",
  "agencyName": "测试代理机构",
  "region": "北京",
  "budgetAmount": 100000,
  "publishTime": "2026-06-22T09:00:00",
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.91,
  "packages": [
    {
      "lotNo": "",
      "lotName": "综合服务",
      "packageNo": "包1",
      "packageName": "测试包件",
      "category": "Service",
      "quantity": null,
      "unit": "",
      "budgetAmount": 100000,
      "maxPrice": 100000,
      "deliveryPlace": "北京",
      "deliveryPeriod": "30天",
      "confidence": 0.9,
      "requirements": [
        {
          "requirementType": "Qualification",
          "originalText": "供应商须具有测试资质。",
          "sourcePage": null,
          "isMandatory": true,
          "isRejectRisk": false,
          "requiredEvidenceType": "QualificationDocument",
          "riskLevel": "Medium",
          "aiExplanation": "公开公告要求。",
          "confidence": 0.86
        }
      ]
    }
  ]
}
"""));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:Model"] = "deepseek-v4-pro",
                ["BidOps:Ai:UseForNoticeStaging"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test",
                ["BidOps:CodexCli:Model"] = "gpt-test",
                ["BidOps:CodexCli:ReasoningEffort"] = "high",
                ["BidOps:CodexCli:TimeoutSeconds"] = "120",
                ["BidOps:CodexCli:WorkingDirectory"] = "D:\\code\\Personal\\Atlas"
            })
            .Build();
        var service = new BidOpsStructuredExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsStructuredExtractionService>(),
            codex);

        var extract = await service.ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(
                "国网测试采购项目采购公告",
                "ProcurementAnnouncement",
                "https://example.test/procurement",
                new DateTime(2026, 6, 22, 9, 0, 0),
                "采购编号：872610\n供应商须具有测试资质。",
                "<table><tr><td>包1</td><td>测试包件</td></tr></table>",
                []));

        Assert.Equal("国网测试采购项目", extract.ProjectName);
        Assert.Equal("872610", extract.ProjectCode);
        var package = Assert.Single(extract.Packages);
        Assert.Equal("包1", package.PackageNo);
        Assert.Equal("供应商须具有测试资质。", Assert.Single(package.Requirements).OriginalText);
        var request = Assert.Single(codex.Requests);
        Assert.Equal("NoticeStaging", request.Use);
        Assert.Equal("codex-test", request.BinaryPath);
        Assert.Equal("gpt-test", request.Model);
        Assert.Equal("high", request.ReasoningEffort);
        Assert.Equal("read-only", request.Sandbox);
        Assert.True(request.SkipGitRepoCheck);
        Assert.True(request.IgnoreRules);
        Assert.True(request.Ephemeral);
        Assert.Contains("不要读取工作目录文件", request.Prompt);
        Assert.Contains("国网测试采购项目采购公告", request.Prompt);
        Assert.Contains("\"packages\"", request.OutputSchemaJson);
    }

    [Fact]
    public async Task BidOpsStructuredExtractionService_DefaultsToCodexCliModelAndReasoningEffort()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            1234,
            "{}",
            string.Empty,
            """
{
  "noticeType": "ProcurementAnnouncement",
  "projectName": "国网默认模型测试",
  "projectCode": "872610",
  "buyerName": "",
  "agencyName": "",
  "region": "",
  "budgetAmount": null,
  "publishTime": null,
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.8,
  "packages": []
}
"""));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:UseForNoticeStaging"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test"
            })
            .Build();
        var service = new BidOpsStructuredExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsStructuredExtractionService>(),
            codex);

        var extract = await service.ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(
                "国网默认模型测试采购公告",
                "ProcurementAnnouncement",
                "https://example.test/default",
                null,
                "采购编号：872610",
                string.Empty,
                []));

        Assert.Equal("国网默认模型测试", extract.ProjectName);
        var request = Assert.Single(codex.Requests);
        Assert.Equal("gpt-5.5", request.Model);
        Assert.Equal("low", request.ReasoningEffort);
    }

    [Fact]
    public async Task BidOpsStructuredExtractionService_UsesRuntimeCodexCliModelAndReasoningEffort()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            1234,
            "{}",
            string.Empty,
            """
{
  "noticeType": "ProcurementAnnouncement",
  "projectName": "运行时模型测试",
  "projectCode": "872610",
  "buyerName": "",
  "agencyName": "",
  "region": "",
  "budgetAmount": null,
  "publishTime": null,
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.8,
  "packages": []
}
"""));
        var aiSettings = new Mock<IBidOpsAiSettingsService>(MockBehavior.Strict);
        aiSettings
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BidOpsAiProviderSettingsDto
            {
                EffectiveProvider = BidOpsSystemValues.AiProviderCodexCli,
                CodexCliModel = "gpt-runtime",
                CodexCliReasoningEffort = "low"
            });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:UseForNoticeStaging"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test",
                ["BidOps:CodexCli:Model"] = "gpt-config",
                ["BidOps:CodexCli:ReasoningEffort"] = "xhigh"
            })
            .Build();
        var service = new BidOpsStructuredExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsStructuredExtractionService>(),
            codex,
            aiSettings.Object);

        await service.ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(
                "运行时模型测试采购公告",
                "ProcurementAnnouncement",
                "https://example.test/runtime",
                null,
                "采购编号：872610",
                string.Empty,
                []));

        var request = Assert.Single(codex.Requests);
        Assert.Equal("gpt-runtime", request.Model);
        Assert.Equal("low", request.ReasoningEffort);
        aiSettings.VerifyAll();
    }

    [Fact]
    public async Task BidOpsStructuredExtractionService_UsesReviewerPromptScenarioForReviewerPrompt()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            1234,
            "{}",
            string.Empty,
            """
{
  "noticeType": "ProcurementAnnouncement",
  "projectName": "重解析模型测试",
  "projectCode": "872610",
  "buyerName": "",
  "agencyName": "",
  "region": "",
  "budgetAmount": null,
  "publishTime": null,
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.8,
  "packages": []
}
"""));
        var aiSettings = new Mock<IBidOpsAiSettingsService>(MockBehavior.Strict);
        aiSettings
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BidOpsAiProviderSettingsDto
            {
                EffectiveProvider = BidOpsSystemValues.AiProviderCodexCli,
                CodexCliModel = "gpt-runtime",
                CodexCliReasoningEffort = "low",
                CodexCliScenarios =
                [
                    new BidOpsCodexCliScenarioSettingsDto
                    {
                        Scenario = BidOpsCodexCliScenarios.Default,
                        Model = "gpt-runtime",
                        ReasoningEffort = "low"
                    },
                    new BidOpsCodexCliScenarioSettingsDto
                    {
                        Scenario = BidOpsCodexCliScenarios.ReviewerPrompt,
                        Model = "gpt-review",
                        ReasoningEffort = "xhigh"
                    }
                ]
            });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:UseForNoticeStaging"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test"
            })
            .Build();
        var service = new BidOpsStructuredExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsStructuredExtractionService>(),
            codex,
            aiSettings.Object);

        await service.ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(
                "重解析模型测试采购公告",
                "ProcurementAnnouncement",
                "https://example.test/reparse",
                null,
                "采购编号：872610",
                string.Empty,
                [],
                "请重新识别包件金额"));

        var request = Assert.Single(codex.Requests);
        Assert.Equal("gpt-review", request.Model);
        Assert.Equal("xhigh", request.ReasoningEffort);
        aiSettings.VerifyAll();
    }

    [Fact]
    public async Task BidOpsStructuredExtractionService_UsesManualReparseScenarioWithoutPrompt()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            1234,
            "{}",
            string.Empty,
            """
{
  "noticeType": "ProcurementAnnouncement",
  "projectName": "无提示重解析模型测试",
  "projectCode": "872610",
  "buyerName": "",
  "agencyName": "",
  "region": "",
  "budgetAmount": null,
  "publishTime": null,
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.8,
  "packages": []
}
"""));
        var aiSettings = new Mock<IBidOpsAiSettingsService>(MockBehavior.Strict);
        aiSettings
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BidOpsAiProviderSettingsDto
            {
                EffectiveProvider = BidOpsSystemValues.AiProviderCodexCli,
                CodexCliModel = "gpt-runtime",
                CodexCliReasoningEffort = "low",
                CodexCliScenarios =
                [
                    new BidOpsCodexCliScenarioSettingsDto
                    {
                        Scenario = BidOpsCodexCliScenarios.Default,
                        Model = "gpt-runtime",
                        ReasoningEffort = "low"
                    },
                    new BidOpsCodexCliScenarioSettingsDto
                    {
                        Scenario = BidOpsCodexCliScenarios.ManualReparse,
                        Model = "gpt-reparse",
                        ReasoningEffort = "medium"
                    }
                ]
            });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:UseForNoticeStaging"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test"
            })
            .Build();
        var service = new BidOpsStructuredExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsStructuredExtractionService>(),
            codex,
            aiSettings.Object);

        await service.ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(
                "无提示重解析模型测试采购公告",
                "ProcurementAnnouncement",
                "https://example.test/reparse-no-prompt",
                null,
                "采购编号：872610",
                string.Empty,
                [],
                IsReparse: true));

        var request = Assert.Single(codex.Requests);
        Assert.Equal("gpt-reparse", request.Model);
        Assert.Equal("medium", request.ReasoningEffort);
        aiSettings.VerifyAll();
    }

    [Fact]
    public async Task BidOpsStructuredExtractionService_UsesComplexScenarioForLongSource()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            1234,
            "{}",
            string.Empty,
            """
{
  "noticeType": "ProcurementAnnouncement",
  "projectName": "复杂件模型测试",
  "projectCode": "872610",
  "buyerName": "",
  "agencyName": "",
  "region": "",
  "budgetAmount": null,
  "publishTime": null,
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.8,
  "packages": []
}
"""));
        var aiSettings = new Mock<IBidOpsAiSettingsService>(MockBehavior.Strict);
        aiSettings
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BidOpsAiProviderSettingsDto
            {
                EffectiveProvider = BidOpsSystemValues.AiProviderCodexCli,
                CodexCliModel = "gpt-runtime",
                CodexCliReasoningEffort = "low",
                CodexCliScenarios =
                [
                    new BidOpsCodexCliScenarioSettingsDto
                    {
                        Scenario = BidOpsCodexCliScenarios.Default,
                        Model = "gpt-runtime",
                        ReasoningEffort = "low"
                    },
                    new BidOpsCodexCliScenarioSettingsDto
                    {
                        Scenario = BidOpsCodexCliScenarios.Complex,
                        Model = "gpt-complex",
                        ReasoningEffort = "medium"
                    }
                ]
            });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:UseForNoticeStaging"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test"
            })
            .Build();
        var service = new BidOpsStructuredExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsStructuredExtractionService>(),
            codex,
            aiSettings.Object);

        await service.ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(
                "复杂件模型测试采购公告",
                "ProcurementAnnouncement",
                "https://example.test/complex",
                null,
                new string('采', 60_000),
                string.Empty,
                []));

        var request = Assert.Single(codex.Requests);
        Assert.Equal("gpt-complex", request.Model);
        Assert.Equal("medium", request.ReasoningEffort);
        aiSettings.VerifyAll();
    }

    [Fact]
    public void BidOpsCodexCliClient_ResolvesWindowsCmdShimBeforeExtensionlessFile()
    {
        if (!OperatingSystem.IsWindows())
            return;

        var directory = Path.Combine(Path.GetTempPath(), $"AtlasCodexPathTest-{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        try
        {
            var extensionlessShim = Path.Combine(directory, "codex");
            var cmdShim = Path.Combine(directory, "codex.cmd");
            File.WriteAllText(extensionlessShim, string.Empty);
            File.WriteAllText(cmdShim, "@echo off");

            var method = typeof(BidOpsCodexCliClient).GetMethod(
                "ResolveBinaryPath",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);
            var resolved = Assert.IsType<string>(method!.Invoke(null, new object?[] { "codex", directory }));

            Assert.Equal(cmdShim, resolved);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact]
    public async Task BidOpsCodexCliClient_WritesOutputSchemaWithoutUtf8Bom()
    {
        var path = Path.Combine(Path.GetTempPath(), $"AtlasCodexSchema-{Guid.NewGuid():N}.json");
        try
        {
            var method = typeof(BidOpsCodexCliClient).GetMethod(
                "WriteUtf8NoBomAsync",
                BindingFlags.Static | BindingFlags.NonPublic);

            Assert.NotNull(method);
            var task = Assert.IsAssignableFrom<Task>(method!.Invoke(
                null,
                new object?[] { path, "\uFEFF{\"type\":\"object\"}", CancellationToken.None }));
            await task;

            var bytes = await File.ReadAllBytesAsync(path);
            Assert.True(bytes.Length > 0);
            Assert.Equal((byte)'{', bytes[0]);
            Assert.False(bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF);
        }
        finally
        {
            if (File.Exists(path))
                File.Delete(path);
        }
    }

    [Fact]
    public async Task BidOpsOutcomeSupplierAiExtractionService_UsesCodexCliProvider()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            2345,
            "{\"records\":[]}",
            "progress",
            """
{
  "records": [
    {
      "supplierName": "北京乙科技有限公司",
      "outcomeType": "Awarded",
      "rank": 1,
      "awardAmount": 1234500,
      "procurementAgencyServiceFeeAmount": 1234.56,
      "projectName": "国网测试项目",
      "projectCode": "872610",
      "buyerName": "国网测试采购单位",
      "lotNo": "",
      "lotName": "综合服务",
      "packageNo": "包1",
      "packageName": "测试包件",
      "category": "服务",
      "evidenceText": "包1 成交人 北京乙科技有限公司",
      "confidence": 0.93
    }
  ]
}
"""));
        var diagnostics = new BidOpsAiCallDiagnostics();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:Model"] = "deepseek-v4-pro",
                ["BidOps:Ai:UseForOutcomeSuppliers"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test",
                ["BidOps:CodexCli:Model"] = "gpt-outcome",
                ["BidOps:CodexCli:ReasoningEffort"] = "xhight",
                ["BidOps:CodexCli:ApiKey"] = "codex-key"
            })
            .Build();
        var service = new BidOpsOutcomeSupplierAiExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            diagnostics,
            new CapturingLogger<BidOpsOutcomeSupplierAiExtractionService>(),
            codex);

        var records = await service.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                "国网测试项目成交结果公告",
                "AwardAnnouncement",
                "https://example.test/result",
                new DateTime(2026, 6, 22),
                "采购编号：872610\n包1 成交人 北京乙科技有限公司",
                [],
                Html: "<table><tr><td>包1</td><td>北京乙科技有限公司</td></tr></table>",
                Attachments:
                [
                    new BidOpsAiAttachmentInput(
                        "成交结果公告.pdf",
                        "pdf",
                        "https://example.test/result.pdf",
                        12345,
                        "包1 成交人 北京乙科技有限公司")
                ]));

        var record = Assert.Single(records);
        Assert.Equal("北京乙科技有限公司", record.SupplierName);
        Assert.Equal("包1", record.PackageNo);
        Assert.Equal(1234500m, record.AwardAmount);
        var request = Assert.Single(codex.Requests);
        Assert.Equal("OutcomeSuppliers", request.Use);
        Assert.Equal("gpt-outcome", request.Model);
        Assert.Equal("xhigh", request.ReasoningEffort);
        Assert.Equal("codex-key", request.ApiKey);
        Assert.Contains("公开中标/成交/候选厂家明细抽取", request.Prompt);
        Assert.Contains("成交结果公告.pdf", request.Prompt);
        Assert.Contains("\"records\"", request.OutputSchemaJson);
        var diagnostic = Assert.Single(diagnostics.Entries);
        Assert.Equal("CodexCli", diagnostic.Provider);
        Assert.Equal("gpt-outcome", diagnostic.Model);
        Assert.Equal(200, diagnostic.StatusCode);
        Assert.Contains("北京乙科技有限公司", diagnostic.AssistantContent);
    }

    [Fact]
    public async Task BidOpsOutcomeSupplierAiExtractionService_CompactsCodexSourceAroundOutcomeEvidence()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            1234,
            "{\"records\":[]}",
            string.Empty,
            "{\"records\":[]}"));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:UseForOutcomeSuppliers"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test",
                ["BidOps:CodexCli:Model"] = "gpt-outcome",
                ["BidOps:CodexCli:MaxInputCharacters"] = "24000"
            })
            .Build();
        var service = new BidOpsOutcomeSupplierAiExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsOutcomeSupplierAiExtractionService>(),
            codex);
        var html = "<table>" +
            string.Concat(Enumerable.Range(0, 600).Select(i => $"<tr><td>无关 HTML 说明 {i:D3}</td></tr>")) +
            "<tr><td>分标编号 10FM03-9001006-0111</td><td>包1</td><td>江苏科能岩土工程有限公司</td></tr>" +
            "<tr><td>HTML_TAIL_SHOULD_NOT_BE_INCLUDED</td></tr></table>";
        var attachmentText = string.Join(
            Environment.NewLine,
            Enumerable.Range(0, 300).Select(i => $"无关附件说明 {i:D3}")) +
            Environment.NewLine +
            "成交结果 分标编号 10FM03-9001006-0111 包1 江苏科能岩土工程有限公司 成交金额 100000元";

        await service.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                "国网测试项目成交结果公告",
                "AwardAnnouncement",
                "https://example.test/result",
                null,
                "采购编号：872610",
                [],
                Html: html,
                Attachments:
                [
                    new BidOpsAiAttachmentInput(
                        "成交结果明细.pdf",
                        "pdf",
                        "https://example.test/result.pdf",
                        12345,
                        attachmentText)
                ]));

        var request = Assert.Single(codex.Requests);
        Assert.Contains("分标编号 10FM03-9001006-0111", request.Prompt);
        Assert.Contains("江苏科能岩土工程有限公司", request.Prompt);
        Assert.Contains("成交结果明细.pdf", request.Prompt);
        Assert.DoesNotContain("HTML_TAIL_SHOULD_NOT_BE_INCLUDED", request.Prompt);
        Assert.True(request.Prompt.Length < 30_000, $"Prompt length was {request.Prompt.Length}.");
    }

    [Fact]
    public async Task BidOpsOutcomeSupplierAiExtractionService_RecordsCodexCliFailureDiagnostics()
    {
        var diagnostics = new BidOpsAiCallDiagnostics();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:UseForOutcomeSuppliers"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test",
                ["BidOps:CodexCli:Model"] = "gpt-outcome"
            })
            .Build();
        var service = new BidOpsOutcomeSupplierAiExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            diagnostics,
            new CapturingLogger<BidOpsOutcomeSupplierAiExtractionService>(),
            new ThrowingCodexCliClient(new InvalidOperationException("codex failed to start")));

        var records = await service.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                "国网测试项目成交结果公告",
                "AwardAnnouncement",
                "https://example.test/result",
                null,
                "包1 成交人 北京乙科技有限公司",
                [],
                ReviewerPrompt: "重新识别成交人"));

        Assert.Empty(records);
        var diagnostic = Assert.Single(diagnostics.Entries);
        Assert.Equal("CodexCli", diagnostic.Provider);
        Assert.Equal("gpt-outcome", diagnostic.Model);
        Assert.Equal(-1, diagnostic.StatusCode);
        Assert.Equal("exception:InvalidOperationException", diagnostic.FinishReason);
        Assert.Contains("codex failed to start", diagnostic.RawResponseBody);
    }

    [Fact]
    public async Task BidOpsOutcomeSupplierAiExtractionService_UsesReviewerPromptScenarioForReviewerPrompt()
    {
        var codex = new FakeCodexCliClient(new BidOpsCodexCliResult(
            0,
            2345,
            "{\"records\":[]}",
            string.Empty,
            """
{
  "records": [
    {
      "supplierName": "北京乙科技有限公司",
      "outcomeType": "Awarded",
      "rank": 1,
      "awardAmount": 1234500,
      "procurementAgencyServiceFeeAmount": null,
      "projectName": "",
      "projectCode": "872610",
      "buyerName": "",
      "lotNo": "",
      "lotName": "综合服务",
      "packageNo": "包1",
      "packageName": "",
      "category": "",
      "evidenceText": "包1 成交人 北京乙科技有限公司",
      "confidence": 0.93
    }
  ]
}
"""));
        var aiSettings = new Mock<IBidOpsAiSettingsService>(MockBehavior.Strict);
        aiSettings
            .Setup(x => x.GetSettingsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(new BidOpsAiProviderSettingsDto
            {
                EffectiveProvider = BidOpsSystemValues.AiProviderCodexCli,
                CodexCliModel = "gpt-runtime",
                CodexCliReasoningEffort = "low",
                CodexCliScenarios =
                [
                    new BidOpsCodexCliScenarioSettingsDto
                    {
                        Scenario = BidOpsCodexCliScenarios.Default,
                        Model = "gpt-runtime",
                        ReasoningEffort = "low"
                    },
                    new BidOpsCodexCliScenarioSettingsDto
                    {
                        Scenario = BidOpsCodexCliScenarios.ReviewerPrompt,
                        Model = "gpt-review",
                        ReasoningEffort = "xhigh"
                    }
                ]
            });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:UseForOutcomeSuppliers"] = "true",
                ["BidOps:CodexCli:BinaryPath"] = "codex-test"
            })
            .Build();
        var service = new BidOpsOutcomeSupplierAiExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsOutcomeSupplierAiExtractionService>(),
            codex,
            aiSettings.Object);

        await service.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                "国网测试项目成交结果公告",
                "AwardAnnouncement",
                "https://example.test/result",
                null,
                "采购编号：872610\n包1 成交人 北京乙科技有限公司",
                [],
                ReviewerPrompt: "重新识别成交人"));

        var request = Assert.Single(codex.Requests);
        Assert.Equal("gpt-review", request.Model);
        Assert.Equal("xhigh", request.ReasoningEffort);
        aiSettings.VerifyAll();
    }

    [Fact]
    public async Task BidOpsOutcomeSupplierAiExtractionService_ExtractsDeepSeekJsonRecords()
    {
        string? capturedBody = null;
        Uri? capturedUri = null;
        var assistantJson = """
{
  "records": [
    {
      "supplierName": "北京乙科技有限公司",
      "outcomeType": "Awarded",
      "rank": 1,
      "awardAmount": "123.45万元",
      "procurementAgencyServiceFeeAmount": "1234.56元",
      "projectName": "国网测试项目",
      "projectCode": "872610",
      "buyerName": "国网测试采购单位",
      "lotNo": "9001005-9999",
      "lotName": "综合服务",
      "packageNo": "1",
      "packageName": "测试包件",
      "category": "服务",
      "evidenceText": "包1 成交人 北京乙科技有限公司 采购代理服务费1234.56元",
      "confidence": 0.91
    }
  ]
}
""";
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = assistantJson
                    }
                }
            }
        });
        var handler = new StubHttpMessageHandler(async (request, ct) =>
        {
            capturedUri = request.RequestUri;
            capturedBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(ct);

            Assert.Equal("Bearer", request.Headers.Authorization?.Scheme);
            Assert.Equal("test-key", request.Headers.Authorization?.Parameter);

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:Provider"] = "DeepSeek",
                ["BidOps:Ai:ApiKey"] = "test-key",
                ["BidOps:Ai:BaseUrl"] = "https://api.deepseek.com",
                ["BidOps:Ai:Model"] = "deepseek-v4-pro",
                ["BidOps:Ai:UseForOutcomeSuppliers"] = "true"
            })
            .Build();
        var logger = new CapturingLogger<BidOpsOutcomeSupplierAiExtractionService>();
        var diagnostics = new BidOpsAiCallDiagnostics();
        var service = new BidOpsOutcomeSupplierAiExtractionService(
            new HttpClient(handler),
            configuration,
            diagnostics,
            logger);

        var records = await service.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                "国网测试项目成交结果公告",
                "AwardAnnouncement",
                "https://example.test/notice",
                new DateTime(2026, 6, 14),
                "采购编号：872610\n包1 成交人 北京乙科技有限公司 采购代理服务费1234.56元",
                [],
                "表格中的最终报价单位是万元，采购编号在正文里。",
                "<table class=\"MsoNormalTable\"><tr><td>包1</td><td>北京乙科技有限公司</td></tr></table>",
                [
                    new BidOpsAiAttachmentInput(
                        "成交结果公告.pdf",
                        "pdf",
                        "https://example.test/result.pdf",
                        12345,
                        "附件表格：包1 成交人 北京乙科技有限公司")
                ]));

        var record = Assert.Single(records);
        Assert.Equal(new Uri("https://api.deepseek.com/chat/completions"), capturedUri);
        Assert.Contains("\"response_format\"", capturedBody);
        using var capturedDocument = JsonDocument.Parse(capturedBody!);
        var capturedSystemPrompt = capturedDocument.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString();
        var capturedPrompt = capturedDocument.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString();
        Assert.Contains("公开中文采购中标/成交结果公告", capturedSystemPrompt!);
        Assert.Contains("不要输出推理过程", capturedSystemPrompt!);
        Assert.Contains("不要输出推理过程", capturedPrompt!);
        Assert.Equal("deepseek-v4-pro", capturedDocument.RootElement.GetProperty("model").GetString());
        Assert.False(capturedDocument.RootElement.TryGetProperty("max_tokens", out _));
        Assert.Contains("表格中的最终报价单位是万元", capturedPrompt!);
        Assert.Contains("MsoNormalTable", capturedPrompt!);
        Assert.Contains("PDF/表格只有包号和厂家时 lotNo 必须返回空字符串", capturedPrompt!);
        Assert.Contains("不要把公告标题、公告名称、采购批次名称、分标名称、包名称或附件文件名放入 projectName", capturedPrompt!);
        Assert.Contains("成交结果公告.pdf", capturedPrompt!);
        Assert.Contains("https://example.test/result.pdf", capturedPrompt!);
        Assert.DoesNotContain("https://example.test/notice", capturedPrompt!);
        var logs = string.Join('\n', logger.Messages);
        Assert.Contains("request body before DeepSeek call", logs);
        Assert.Contains(capturedBody!, logs);
        Assert.Contains("公开中文采购中标/成交结果公告", capturedBody!);
        Assert.DoesNotContain("\\u4", capturedBody!);
        Assert.Contains("raw DeepSeek response", logs);
        Assert.Contains("北京乙科技有限公司", logs);
        Assert.DoesNotContain("\\u5317\\u4eac", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key", logs);
        var diagnostic = Assert.Single(diagnostics.Entries);
        Assert.Equal("OutcomeSuppliers", diagnostic.Use);
        Assert.Equal("DeepSeek", diagnostic.Provider);
        Assert.Equal("deepseek-v4-pro", diagnostic.Model);
        Assert.Equal(responseJson, diagnostic.RawResponseBody);
        Assert.Equal(assistantJson, diagnostic.AssistantContent);
        Assert.Equal(responseJson.Length, diagnostic.ResponseCharacters);
        Assert.Equal(assistantJson.Length, diagnostic.AssistantCharacters);
        Assert.Equal("北京乙科技有限公司", record.SupplierName);
        Assert.Equal(BidOpsOutcomeTypes.Awarded, record.OutcomeType);
        Assert.Equal(1234500m, record.AwardAmount);
        Assert.Equal(1234.56m, record.ProcurementAgencyServiceFeeAmount);
        Assert.Equal("872610", record.ProjectCode);
        Assert.Equal("9001005-9999", record.LotNo);
        Assert.Equal("包1", record.PackageNo);
        Assert.Equal(0, record.ExtractionOrder);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_ReviewerPromptUsesAiRowsAsReplacement()
    {
        var deterministic = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "旧规则厂家有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包1",
                Confidence = 0.6m
            }
        };
        var ai = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "DeepSeek修正厂家有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包1",
                Confidence = 0.95m
            }
        };
        var method = typeof(BidOpsOutcomeSupplierExtractionService).GetMethod(
            "ChooseOutcomeExtractsForPersistence",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var selected = (IReadOnlyList<BidOpsOutcomeSupplierExtract>)method!.Invoke(
            null,
            new object?[] { deterministic, ai, "用附件 PDF 表格重新解析" })!;

        var record = Assert.Single(selected);
        Assert.Equal("DeepSeek修正厂家有限公司", record.SupplierName);
        Assert.Equal(0, record.ExtractionOrder);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_ReviewerPromptPreservesAiAnnouncementOrder()
    {
        var ai = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "公告第一行厂家有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包2",
                Confidence = 0.95m
            },
            new()
            {
                SupplierName = "公告第二行厂家有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包1",
                Confidence = 0.95m
            }
        };
        var method = typeof(BidOpsOutcomeSupplierExtractionService).GetMethod(
            "ChooseOutcomeExtractsForPersistence",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var selected = (IReadOnlyList<BidOpsOutcomeSupplierExtract>)method!.Invoke(
            null,
            new object?[] { Array.Empty<BidOpsOutcomeSupplierExtract>(), ai, "以公告顺序为准" })!;

        Assert.Collection(
            selected,
            first =>
            {
                Assert.Equal("公告第一行厂家有限公司", first.SupplierName);
                Assert.Equal(0, first.ExtractionOrder);
            },
            second =>
            {
                Assert.Equal("公告第二行厂家有限公司", second.SupplierName);
                Assert.Equal(1, second.ExtractionOrder);
            });
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_AutomaticMergePrioritizesAiAnnouncementOrder()
    {
        var deterministic = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "公告第二行厂家有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包1",
                Confidence = 0.6m
            },
            new()
            {
                SupplierName = "确定性补充厂家有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包3",
                Confidence = 0.7m
            }
        };
        var ai = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "公告第一行厂家有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包2",
                Confidence = 0.95m
            },
            new()
            {
                SupplierName = "公告第二行厂家有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包1",
                AwardAmount = 120000m,
                Confidence = 0.95m
            }
        };
        var method = typeof(BidOpsOutcomeSupplierExtractionService).GetMethod(
            "ChooseOutcomeExtractsForPersistence",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var selected = (IReadOnlyList<BidOpsOutcomeSupplierExtract>)method!.Invoke(
            null,
            new object?[] { deterministic, ai, null })!;

        Assert.Collection(
            selected,
            first =>
            {
                Assert.Equal("公告第一行厂家有限公司", first.SupplierName);
                Assert.Equal(0, first.ExtractionOrder);
            },
            second =>
            {
                Assert.Equal("公告第二行厂家有限公司", second.SupplierName);
                Assert.Equal(120000m, second.AwardAmount);
                Assert.Equal(1, second.ExtractionOrder);
            },
            third =>
            {
                Assert.Equal("确定性补充厂家有限公司", third.SupplierName);
                Assert.Equal(2, third.ExtractionOrder);
            });
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_KeepsSameSupplierRowsWithDifferentLotNames()
    {
        var ai = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "北京乙科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                LotNo = "9001005-9999",
                LotName = "综合服务",
                PackageNo = "包1",
                EvidenceText = "综合服务 包1 北京乙科技有限公司",
                Confidence = 0.95m
            },
            new()
            {
                SupplierName = "北京乙科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                LotNo = "9001005-9999",
                LotName = "运维服务",
                PackageNo = "包1",
                EvidenceText = "运维服务 包1 北京乙科技有限公司",
                Confidence = 0.95m
            }
        };
        var method = typeof(BidOpsOutcomeSupplierExtractionService).GetMethod(
            "ChooseOutcomeExtractsForPersistence",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var selectedByReviewerPrompt = (IReadOnlyList<BidOpsOutcomeSupplierExtract>)method!.Invoke(
            null,
            new object?[] { Array.Empty<BidOpsOutcomeSupplierExtract>(), ai, "按公告行保存" })!;
        var selectedByAutomaticMerge = (IReadOnlyList<BidOpsOutcomeSupplierExtract>)method.Invoke(
            null,
            new object?[] { Array.Empty<BidOpsOutcomeSupplierExtract>(), ai, null })!;

        Assert.Equal(2, selectedByReviewerPrompt.Count);
        Assert.Equal(2, selectedByAutomaticMerge.Count);
        Assert.Contains(selectedByReviewerPrompt, x => x.LotName == "综合服务" && x.PackageNo == "包1");
        Assert.Contains(selectedByReviewerPrompt, x => x.LotName == "运维服务" && x.PackageNo == "包1");
        Assert.Contains(selectedByAutomaticMerge, x => x.LotName == "综合服务" && x.PackageNo == "包1");
        Assert.Contains(selectedByAutomaticMerge, x => x.LotName == "运维服务" && x.PackageNo == "包1");
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_ClearsUnsupportedAiLotNo()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "北京乙科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                LotNo = "9001005-9999",
                LotName = "综合服务",
                PackageNo = "包1",
                EvidenceText = "综合服务 包1 北京乙科技有限公司",
                Confidence = 0.95m
            }
        };
        const string sourceText = """
国网测试项目成交结果公告
采购编号：9001005-9999
分标名称 包号 成交人
综合服务 包1 北京乙科技有限公司
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal(string.Empty, record.LotNo);
        Assert.Equal("综合服务", record.LotName);
        Assert.Equal("包1", record.PackageNo);
        Assert.Equal(0.78m, record.Confidence);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_KeepsLotNoWhenSourceHasExplicitLotHeader()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "北京乙科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                LotNo = "9001005-9999",
                PackageNo = "包1",
                EvidenceText = "9001005-9999 包1 北京乙科技有限公司",
                Confidence = 0.95m
            }
        };
        const string sourceText = """
国网测试项目成交结果公告
采购编号：P-001
分标编号 包号 成交人
9001005-9999 包1 北京乙科技有限公司
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("9001005-9999", record.LotNo);
        Assert.Equal(0.95m, record.Confidence);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_FillsLotNoFromLeadingOutcomeEvidence()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "保定德优电气设备制造有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包 1",
                EvidenceText = "122609-9204013-9999 包 1 保定德优电气设备制造有限公司",
                Confidence = 0.95m
            }
        };
        const string sourceText = """
国网测试项目中标结果公告
分标编号 包号 中标人
122609-9204013-9999 包 1 保定德优电气设备制造有限公司
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("122609-9204013-9999", record.LotNo);
        Assert.Equal("包 1", record.PackageNo);
        Assert.Equal(0.78m, record.Confidence);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_FillsLotNoFromOrdinalPrefixedOutcomeEvidence()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "江苏科能岩土工程有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包 1",
                EvidenceText = "1 10FM03-9001006-0111 包 1 江苏科能岩土工程有限公司",
                Confidence = 0.95m
            }
        };
        const string sourceText = """
国网江苏电力泰州供电公司2026年第三次服务授权公开谈判采购项目成交结果公告
分标编号 包号 成交人
1 10FM03-9001006-0111 包 1 江苏科能岩土工程有限公司
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("10FM03-9001006-0111", record.LotNo);
        Assert.Equal("包 1", record.PackageNo);
        Assert.Equal(0.78m, record.Confidence);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_ClearsUnsupportedProjectNameFromNoticeTitle()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "北京乙科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                ProjectName = "国网测试服务采购成交结果公告",
                LotName = "综合服务",
                PackageNo = "包1",
                EvidenceText = "综合服务 包1 北京乙科技有限公司",
                Confidence = 0.95m
            }
        };
        const string sourceText = """
标题：国网测试服务采购成交结果公告
采购编号：P-001
分标名称 包号 成交人
综合服务 包1 北京乙科技有限公司
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal(string.Empty, record.ProjectName);
        Assert.Equal("综合服务", record.LotName);
        Assert.Equal(0.95m, record.Confidence);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_KeepsProjectNameWhenSourceHasExplicitProjectHeader()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "北京乙科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                ProjectName = "变电站消防手续报件和验收备案服务框架采购",
                LotName = "综合服务",
                PackageNo = "包1",
                EvidenceText = "综合服务 包1 变电站消防手续报件和验收备案服务框架采购 北京乙科技有限公司",
                Confidence = 0.95m
            }
        };
        const string sourceText = """
标题：国网测试服务采购成交结果公告
采购编号：P-001
分标名称 包号 项目名称 成交人
综合服务 包1 变电站消防手续报件和验收备案服务框架采购 北京乙科技有限公司
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("变电站消防手续报件和验收备案服务框架采购", record.ProjectName);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_KeepsProjectNameWhenSourceHasProjectNameTableHeader()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "山西衡电检测科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                ProjectName = "国网长治供电公司2026年至2027年输变电工程质量检测服务",
                LotName = "电网工程服务框架-长治",
                PackageNo = "包 01",
                EvidenceText = "1. 电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司 标准单价×93.00%",
                Confidence = 0.9m
            }
        };
        const string sourceText = """
        序号 分标名称 包号 项目名称 成交单位
        1. 电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司 标准单价×93.00%
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("国网长治供电公司2026年至2027年输变电工程质量检测服务", record.ProjectName);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_KeepsProjectNameWhenPdfWrapsProjectNameLines()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "山西衡电检测科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                ProjectName = "国网长治供电公司2026年至2027年输变电工程质量检测服务",
                LotName = "电网工程服务框架-长治",
                PackageNo = "包 01",
                EvidenceText = "电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司",
                Confidence = 0.95m
            }
        };
        const string sourceText = """
序号 分标名称 包号 项目名称 成交单位 成交金额
1. 电网工程服务框架-
长治 包 01 国网长治供电公司2026年至2027年输
变电工程质量检测服务 山西衡电检测科技有限公司 标准单价×93.00%
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("国网长治供电公司2026年至2027年输变电工程质量检测服务", record.ProjectName);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_ClearsProjectNameWithoutProjectNameLabel()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "山西衡电检测科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                ProjectName = "国网长治供电公司2026年至2027年输变电工程质量检测服务",
                LotName = "电网工程服务框架-长治",
                PackageNo = "包 01",
                EvidenceText = "1. 电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司 标准单价×93.00%",
                Confidence = 0.9m
            }
        };
        const string sourceText = """
序号 分标名称 包号 成交单位
1. 电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司 标准单价×93.00%
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal(string.Empty, record.ProjectName);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_PromotesProjectNameColumnValueFromPackageName()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "山西衡电检测科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                ProjectName = "国网山西电力2026年南部片区分公司第三次服务框架竞争性谈判授权联合采购项目",
                LotName = "电网工程服务框架-长治",
                PackageNo = "包 01",
                PackageName = "国网长治供电公司2026年至2027年输变电工程质量检测服务",
                EvidenceText = "电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司 标准单价×93.00%",
                Confidence = 0.9m
            }
        };
        const string sourceText = """
序号 分标名称 包号 项目名称 成交单位 成交金额
1. 电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司 标准单价×93.00%
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("国网长治供电公司2026年至2027年输变电工程质量检测服务", record.ProjectName);
        Assert.Equal(string.Empty, record.PackageName);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_ClearsPackageNameWhenItEqualsProjectName()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "山西衡电检测科技有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                ProjectName = "国网长治供电公司2026年至2027年输变电工程质量检测服务",
                LotName = "电网工程服务框架-长治",
                PackageNo = "包 01",
                PackageName = "国网长治供电公司2026年至2027年输变电工程质量检测服务",
                EvidenceText = "电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司",
                Confidence = 0.95m
            }
        };
        const string sourceText = """
序号 分标名称 包号 项目名称 成交单位 成交金额
1. 电网工程服务框架-长治 包 01 国网长治供电公司2026年至2027年输变电工程质量检测服务 山西衡电检测科技有限公司 标准单价×93.00%
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("国网长治供电公司2026年至2027年输变电工程质量检测服务", record.ProjectName);
        Assert.Equal(string.Empty, record.PackageName);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_SkipsPackageFallbackWhenItEqualsProjectName()
    {
        var method = typeof(BidOpsOutcomeSupplierExtractionService).GetMethod(
            "FirstPackageNameDistinctFromProject",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var packageName = Assert.IsType<string>(method!.Invoke(
            null,
            new object?[]
            {
                "国网长治供电公司2026年至2027年输变电工程质量检测服务",
                new string?[]
                {
                    "国网长治供电公司2026年至2027年输变电工程质量检测服务",
                    "包1输变电工程检测"
                }
            }));

        Assert.Equal("包1输变电工程检测", packageName);
    }

    [Fact]
    public void BidOpsWrappedOutcomeTableParser_DoesNotUseTitleAsProjectNameWithoutExplicitProjectName()
    {
        const string title = "国网测试服务采购成交结果公告";
        const string sourceText = """
采购编号：P-001
现将成交人名单公告如下：
序号 分标名称 包号 成交单位 成交金额
1 综合服务 包 01 北京乙科技有限公司 10 万元
""";

        var records = BidOpsWrappedOutcomeTableParser.Extract(title, "AwardAnnouncement", sourceText);

        var record = Assert.Single(records);
        Assert.Equal(string.Empty, record.ProjectName);
        Assert.Equal("北京乙科技有限公司", record.SupplierName);
    }

    [Fact]
    public void BidOpsAwardEvidenceParser_DoesNotUseDocumentTitleAsProjectNameWhenNoExplicitColumn()
    {
        const string title = "国网测试服务采购成交结果公告";
        const string sourceText = """
采购编号：P-001
| 分标名称 | 包号 | 成交人 |
| 综合服务 | 包1 | 北京乙科技有限公司 |
""";

        var records = BidOpsAwardEvidenceParser.Extract([
            new BidOpsEvidenceDocument(
                new EvidenceSourceRef(1, null, "AwardAnnouncement", "https://example.test", null, null, null, null, null),
                title,
                "AwardAnnouncement",
                null,
                sourceText)
        ]);

        var record = Assert.Single(records);
        Assert.True(string.IsNullOrWhiteSpace(record.ProjectName));
        Assert.Equal("北京乙科技有限公司", record.AwardedSupplierName);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_FiltersMisalignedFallbackSupplierRows()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "kV",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包01",
                EvidenceText = "山西大同广灵南村 110kV 输变电工程建设用地",
                Confidence = 0.88m
            },
            new()
            {
                SupplierName = "43",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包06",
                EvidenceText = "朗新科技集团股份有限公司 124.43 万元",
                Confidence = 0.88m
            },
            new()
            {
                SupplierName = "万元",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "合邦电力科技有限公司",
                EvidenceText = "合邦电力科技有限公司 36.42 万元",
                Confidence = 0.88m
            },
            new()
            {
                SupplierName = "国网朔州供电公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包01",
                EvidenceText = "零星服务-朔州 包 01 国网朔州供电公司 2026 年电梯维护保养 本次流标 /",
                Confidence = 0.72m
            },
            new()
            {
                SupplierName = "山西宏欣土地咨询服务有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包03",
                EvidenceText = "电网工程服务-大同 包 03 山西宏欣土地咨询服务有限公司 49 万元",
                Confidence = 0.93m
            }
        };
        const string sourceText = """
序号 分标名称 包号 项目名称 成交单位 成交金额
1 电网工程服务-大同 包 03 山西大同广灵南村 110kV 输变电工程土地确权服务 山西宏欣土地咨询服务有限公司 49 万元
""";

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, sourceText);

        var record = Assert.Single(selected);
        Assert.Equal("山西宏欣土地咨询服务有限公司", record.SupplierName);
        Assert.Equal("包03", record.PackageNo);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_KeepsValidOrganizationNamesWithoutAmount()
    {
        var extracts = new List<BidOpsOutcomeSupplierExtract>
        {
            new()
            {
                SupplierName = "山西光硕律师事务所",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包01",
                EvidenceText = "零星服务-大同 包 01 国网大同供电公司 2026 年法律顾问服务 山西光硕律师事务所",
                Confidence = 0.9m
            },
            new()
            {
                SupplierName = "山西春晖工程勘察设计检测研究院有限公司",
                OutcomeType = BidOpsOutcomeTypes.Awarded,
                PackageNo = "包02",
                EvidenceText = "建设分公司 包 02 忻州北 500kV 输变电工程线路工程压覆矿评估 山西春晖工程勘察设计检测研究院有限公司",
                Confidence = 0.9m
            }
        };

        var selected = SanitizeOutcomeExtractsForPersistence(extracts, string.Empty);

        Assert.Collection(
            selected,
            first => Assert.Equal("山西光硕律师事务所", first.SupplierName),
            second => Assert.Equal("山西春晖工程勘察设计检测研究院有限公司", second.SupplierName));
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractionService_ReviewerPromptForcesAiAttempt()
    {
        var method = typeof(BidOpsOutcomeSupplierExtractionService).GetMethod(
            "ShouldAttemptOutcomeExtraction",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        Assert.True((bool)method!.Invoke(null, new object?[] { false, "请按附件表格重新提取候选人" })!);
        Assert.True((bool)method.Invoke(null, new object?[] { true, null })!);
        Assert.False((bool)method.Invoke(null, new object?[] { false, null })!);
    }

    private static IReadOnlyList<BidOpsOutcomeSupplierExtract> SanitizeOutcomeExtractsForPersistence(
        IReadOnlyList<BidOpsOutcomeSupplierExtract> extracts,
        string sourceText)
    {
        var method = typeof(BidOpsOutcomeSupplierExtractionService).GetMethod(
            "SanitizeOutcomeExtractsForPersistence",
            BindingFlags.Static | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return (IReadOnlyList<BidOpsOutcomeSupplierExtract>)method!.Invoke(
            null,
            new object?[] { extracts, sourceText })!;
    }

    [Fact]
    public async Task BidOpsOutcomeSupplierAiExtractionService_LogsUnavailableSettingsWithoutApiKey()
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:Provider"] = "DeepSeek",
                ["BidOps:Ai:BaseUrl"] = "https://api.deepseek.com",
                ["BidOps:Ai:Model"] = "deepseek-v4-pro",
                ["BidOps:Ai:UseForOutcomeSuppliers"] = "true"
            })
            .Build();
        var logger = new CapturingLogger<BidOpsOutcomeSupplierAiExtractionService>();
        var diagnostics = new BidOpsAiCallDiagnostics();
        var service = new BidOpsOutcomeSupplierAiExtractionService(
            new HttpClient(new StubHttpMessageHandler((_, _) => throw new InvalidOperationException("HTTP should not be called."))),
            configuration,
            diagnostics,
            logger);

        var records = await service.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                "国网测试成交结果公告",
                "AwardAnnouncement",
                string.Empty,
                null,
                "成交供应商：北京乙科技有限公司",
                [],
                "请重新提取",
                string.Empty,
                []));

        Assert.Empty(records);
        var logs = string.Join('\n', logger.Messages);
        Assert.Contains("AI HTTP settings are unavailable", logs);
        Assert.Contains("hasApiKey=False", logs);
        Assert.Contains("apiKeySource=None", logs);
        Assert.Empty(diagnostics.Entries);
    }

    [Fact]
    public async Task BidOpsOutcomeSupplierAiExtractionService_HandlesEmptyDeepSeekContent()
    {
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        role = "assistant",
                        content = string.Empty,
                        reasoning_content = "模型只返回了推理内容，没有返回 JSON。"
                    },
                    finish_reason = "length"
                }
            }
        });
        var handler = new StubHttpMessageHandler((_, _) => Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
        }));
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:Provider"] = "DeepSeek",
                ["BidOps:Ai:ApiKey"] = "test-key",
                ["BidOps:Ai:BaseUrl"] = "https://api.deepseek.com",
                ["BidOps:Ai:Model"] = "deepseek-v4-pro",
                ["BidOps:Ai:UseForOutcomeSuppliers"] = "true"
            })
            .Build();
        var logger = new CapturingLogger<BidOpsOutcomeSupplierAiExtractionService>();
        var diagnostics = new BidOpsAiCallDiagnostics();
        var service = new BidOpsOutcomeSupplierAiExtractionService(
            new HttpClient(handler),
            configuration,
            diagnostics,
            logger);

        var records = await service.ExtractAsync(
            new BidOpsOutcomeSupplierAiExtractionRequest(
                "国网测试成交候选人公示",
                "CandidateAnnouncement",
                string.Empty,
                null,
                "推荐成交候选人：北京乙科技有限公司",
                [],
                "请重新提取",
                string.Empty,
                []));

        Assert.Empty(records);
        var logs = string.Join('\n', logger.Messages);
        Assert.Contains("empty assistant content", logs);
        Assert.Contains("finishReason=length", logs);
        Assert.DoesNotContain("AI extraction failed", logs);
        var diagnostic = Assert.Single(diagnostics.Entries);
        Assert.Equal(responseJson, diagnostic.RawResponseBody);
        Assert.Equal(string.Empty, diagnostic.AssistantContent);
        Assert.Equal("length", diagnostic.FinishReason);
    }

    [Fact]
    public async Task BidOpsStructuredExtractionService_SendsHtmlAndAttachmentsToDeepSeek()
    {
        string? capturedBody = null;
        var assistantJson = """
{
  "noticeType": "AwardAnnouncement",
  "projectName": "国网测试成交结果公告",
  "projectCode": "CG-2026-001",
  "buyerName": "国网测试采购单位",
  "agencyName": "测试代理机构",
  "region": "河南",
  "budgetAmount": null,
  "publishTime": "2026-06-12T00:00:00",
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.91,
  "packages": [
    {
      "lotNo": "LOT-1",
      "lotName": "综合服务",
      "packageNo": "包1",
      "packageName": "测试服务包",
      "category": "Service",
      "quantity": null,
      "unit": "",
      "budgetAmount": null,
      "maxPrice": null,
      "deliveryPlace": "",
      "deliveryPeriod": "",
      "confidence": 0.88,
      "requirements": []
    }
  ]
}
""";
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = assistantJson
                    }
                }
            }
        });
        var handler = new StubHttpMessageHandler(async (request, ct) =>
        {
            capturedBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:Provider"] = "DeepSeek",
                ["BidOps:Ai:ApiKey"] = "test-key",
                ["BidOps:Ai:BaseUrl"] = "https://api.deepseek.com",
                ["BidOps:Ai:Model"] = "deepseek-v4-pro",
                ["BidOps:Ai:UseForNoticeStaging"] = "true"
            })
            .Build();
        var logger = new CapturingLogger<BidOpsStructuredExtractionService>();
        var diagnostics = new BidOpsAiCallDiagnostics();
        var service = new BidOpsStructuredExtractionService(
            new HttpClient(handler),
            configuration,
            diagnostics,
            logger);

        var extract = await service.ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(
                "国网测试成交结果公告",
                "AwardAnnouncement",
                "https://example.test/notice",
                new DateTime(2026, 6, 12),
                "公告正文：采购编号：CG-2026-001",
                "<table class=\"MsoNormalTable\"><tr><td>采购编号</td><td>CG-2026-001</td></tr></table>",
                [
                    new BidOpsAiAttachmentInput(
                        "成交结果附件.pdf",
                        "pdf",
                        "https://example.test/attachment.pdf",
                        45678,
                        "附件内容：分标编号 LOT-1 包1")
                ],
                "优先按审核人员提示修正公告字段"),
            CancellationToken.None);

        using var capturedDocument = JsonDocument.Parse(capturedBody!);
        var capturedSystemPrompt = capturedDocument.RootElement
            .GetProperty("messages")[0]
            .GetProperty("content")
            .GetString();
        var capturedPrompt = capturedDocument.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString();
        Assert.Contains("公开招投标/采购公告", capturedSystemPrompt!);
        Assert.Contains("公告正文 HTML", capturedPrompt!);
        Assert.Contains("MsoNormalTable", capturedPrompt!);
        Assert.Contains("附件 1", capturedPrompt!);
        Assert.Contains("成交结果附件.pdf", capturedPrompt!);
        Assert.Contains("https://example.test/attachment.pdf", capturedPrompt!);
        Assert.Contains("审核人员修正提示", capturedPrompt!);
        Assert.Contains("优先按审核人员提示修正公告字段", capturedPrompt!);
        Assert.DoesNotContain("https://example.test/notice", capturedPrompt!);
        Assert.Contains("AwardAnnouncement 或 ResultAnnouncement", capturedPrompt!);
        var logs = string.Join('\n', logger.Messages);
        Assert.Contains("request body before DeepSeek call", logs);
        Assert.Contains(capturedBody!, logs);
        Assert.Contains("公开招投标/采购公告", capturedBody!);
        Assert.DoesNotContain("\\u4", capturedBody!);
        Assert.Contains("raw DeepSeek response", logs);
        Assert.Contains("国网测试采购单位", logs);
        Assert.DoesNotContain("\\u56fd\\u7f51", logs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("test-key", logs);
        var diagnostic = Assert.Single(diagnostics.Entries);
        Assert.Equal("NoticeStaging", diagnostic.Use);
        Assert.Equal(responseJson, diagnostic.RawResponseBody);
        Assert.Equal(assistantJson, diagnostic.AssistantContent);
        Assert.Equal("CG-2026-001", extract.ProjectCode);
        Assert.Equal("包1", Assert.Single(extract.Packages).PackageNo);
    }

    [Fact]
    public async Task BidOpsStructuredExtractionService_NormalizesProcurementAmountsFromTenThousandYuanHeaders()
    {
        string? capturedBody = null;
        var assistantJson = """
{
  "noticeType": "ProcurementAnnouncement",
  "projectName": "国网测试服务采购公告",
  "projectCode": "19FBAC",
  "buyerName": "国网测试采购单位",
  "agencyName": "",
  "region": "北京",
  "budgetAmount": null,
  "publishTime": null,
  "signupDeadline": null,
  "bidDeadline": null,
  "openBidTime": null,
  "confidence": 0.91,
  "packages": [
    {
      "lotNo": "19FBAC-9013001-3000",
      "lotName": "房屋维修-施工",
      "packageNo": "包1",
      "packageName": "包1房屋维修施工",
      "category": "Service",
      "quantity": null,
      "unit": "",
      "budgetAmount": null,
      "maxPrice": 45.78,
      "deliveryPlace": "北京",
      "deliveryPeriod": "",
      "confidence": 0.88,
      "requirements": []
    }
  ]
}
""";
        var responseJson = JsonSerializer.Serialize(new
        {
            choices = new[]
            {
                new
                {
                    message = new
                    {
                        content = assistantJson
                    }
                }
            }
        });
        var handler = new StubHttpMessageHandler(async (request, ct) =>
        {
            capturedBody = request.Content == null ? string.Empty : await request.Content.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(responseJson, Encoding.UTF8, "application/json")
            };
        });
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["BidOps:Ai:Enabled"] = "true",
                ["BidOps:Ai:Provider"] = "DeepSeek",
                ["BidOps:Ai:ApiKey"] = "test-key",
                ["BidOps:Ai:BaseUrl"] = "https://api.deepseek.com",
                ["BidOps:Ai:Model"] = "deepseek-v4-pro",
                ["BidOps:Ai:UseForNoticeStaging"] = "true"
            })
            .Build();
        var service = new BidOpsStructuredExtractionService(
            new HttpClient(handler),
            configuration,
            new BidOpsAiCallDiagnostics(),
            new CapturingLogger<BidOpsStructuredExtractionService>());

        var attachmentText = """
## 表格 1：Sheet: 采购公告附件
| 采购编号 | 分标编号 | 分标名称 | 包号 | 包名称 | 子项目名称 | 行报价最高限价（含税/万元） | 报价方式 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| 19FBAC | 19FBAC-9013001-3000 | 房屋维修-施工 | 包1 | 包1房屋维修施工 | 门窗维修 | 45.78 | 固定总价 |
""";

        var extract = await service.ExtractAsync(
            new BidOpsNoticeAiExtractionRequest(
                "国网测试服务采购公告",
                "ProcurementAnnouncement",
                "https://example.test/notice",
                null,
                string.Empty,
                string.Empty,
                [
                    new BidOpsAiAttachmentInput(
                        "采购公告附件.xlsx",
                        "xlsx",
                        "https://example.test/attachment.xlsx",
                        12345,
                        attachmentText)
                ]),
            CancellationToken.None);

        using var capturedDocument = JsonDocument.Parse(capturedBody!);
        var capturedPrompt = capturedDocument.RootElement
            .GetProperty("messages")[1]
            .GetProperty("content")
            .GetString();
        Assert.Contains("列名", capturedPrompt!);
        Assert.Contains("万元", capturedPrompt!);
        Assert.Equal(457800m, Assert.Single(extract.Packages).MaxPrice);
    }

    [Fact]
    public async Task StructuredParseJobHandler_ExtractsOutcomeSuppliersWhenNoticeParsingFails()
    {
        var parsing = new Mock<IBidOpsAiParsingService>(MockBehavior.Strict);
        parsing
            .Setup(x => x.ParseRawNoticeAsync(123, null, It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("parse failed"));

        var extraction = new Mock<IBidOpsOutcomeSupplierExtractionService>(MockBehavior.Strict);
        extraction
            .Setup(x => x.ExtractRawNoticeAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutcomeSupplierExtractionResultDto
            {
                RawNoticeId = 123,
                IsOutcomeNotice = true,
                ExtractedCount = 93,
                SavedCount = 93
            });

        var handler = new StructuredParseJobHandler(
            new ExecutionIdentityAccessor(),
            parsing.Object,
            extraction.Object,
            new BidOpsAiCallDiagnostics(),
            NullLogger<StructuredParseJobHandler>.Instance);
        var payload = new StructuredParseJobPayload(300001, null, 42, "bidops", 123);
        var context = new BackgroundJobExecutionContext(new BackgroundJob
        {
            Id = 1,
            Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });

        await Assert.ThrowsAsync<InvalidOperationException>(() => handler.HandleAsync(context));

        extraction.Verify(x => x.ExtractRawNoticeAsync(123, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task StructuredParseJobHandler_ReturnsJsonResultSummary()
    {
        var parsing = new Mock<IBidOpsAiParsingService>(MockBehavior.Strict);
        parsing
            .Setup(x => x.ParseRawNoticeAsync(123, "采购公告提示词", It.IsAny<CancellationToken>()))
            .ReturnsAsync(456);

        var extraction = new Mock<IBidOpsOutcomeSupplierExtractionService>(MockBehavior.Strict);
        extraction
            .Setup(x => x.ExtractRawNoticeAsync(123, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutcomeSupplierExtractionResultDto
            {
                RawNoticeId = 123,
                IsOutcomeNotice = true,
                ExtractedCount = 3,
                SavedCount = 2,
                Message = "saved"
            });

        var diagnostics = new BidOpsAiCallDiagnostics();
        diagnostics.Record(new BidOpsAiCallDiagnosticEntry(
            "NoticeStaging",
            "DeepSeek",
            "deepseek-v4-pro",
            "api.deepseek.com/chat/completions",
            200,
            1234,
            98,
            42,
            "stop",
            "{\"choices\":[{\"message\":{\"content\":\"{\\\"ok\\\":true}\"}}]}",
            "{\"ok\":true}"));

        var handler = new StructuredParseJobHandler(
            new ExecutionIdentityAccessor(),
            parsing.Object,
            extraction.Object,
            diagnostics,
            NullLogger<StructuredParseJobHandler>.Instance);
        var payload = new StructuredParseJobPayload(300001, null, 42, "bidops", 123, "manual", "采购公告提示词");
        var context = new BackgroundJobExecutionContext(new BackgroundJob
        {
            Id = 1,
            Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });

        var result = await handler.HandleAsync(context);

        Assert.True(result.Succeeded);
        Assert.Equal(BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters, result.MaxResultCharacters);
        using var document = JsonDocument.Parse(result.Result!);
        Assert.Equal(123, document.RootElement.GetProperty("rawNoticeId").GetInt64());
        Assert.Equal(456, document.RootElement.GetProperty("reviewTaskId").GetInt64());
        Assert.True(document.RootElement.GetProperty("reviewerPrompt").GetBoolean());
        Assert.Equal(2, document.RootElement
            .GetProperty("outcomeSupplierExtraction")
            .GetProperty("savedCount")
            .GetInt32());
        var aiResponse = Assert.Single(document.RootElement.GetProperty("aiResponses").EnumerateArray());
        Assert.Equal("DeepSeek", aiResponse.GetProperty("provider").GetString());
        var deepSeekResponse = Assert.Single(document.RootElement.GetProperty("deepSeekResponses").EnumerateArray());
        Assert.Equal("DeepSeek", deepSeekResponse.GetProperty("provider").GetString());
        Assert.Contains("choices", deepSeekResponse.GetProperty("rawResponseBody").GetString());
        Assert.Equal("{\"ok\":true}", deepSeekResponse.GetProperty("assistantContent").GetString());
    }

    [Fact]
    public async Task OutcomeSupplierExtractJobHandler_ReturnsJsonResultSummary()
    {
        var extraction = new Mock<IBidOpsOutcomeSupplierExtractionService>(MockBehavior.Strict);
        extraction
            .Setup(x => x.ExtractRawNoticeAsync(123, "提示词", It.IsAny<CancellationToken>()))
            .ReturnsAsync(new OutcomeSupplierExtractionResultDto
            {
                RawNoticeId = 123,
                IsOutcomeNotice = true,
                ExtractedCount = 4,
                SavedCount = 3,
                BuyerCreatedCount = 1,
                SupplierUpdatedCount = 2,
                Message = "已保存 3 条公开结果厂家线索。"
            });

        var diagnostics = new BidOpsAiCallDiagnostics();
        diagnostics.Record(new BidOpsAiCallDiagnosticEntry(
            "OutcomeSuppliers",
            "DeepSeek",
            "deepseek-v4-pro",
            "api.deepseek.com/chat/completions",
            200,
            1567,
            120,
            60,
            "stop",
            "{\"choices\":[{\"message\":{\"content\":\"{\\\"records\\\":[]}\"}}]}",
            "{\"records\":[]}"));

        var handler = new OutcomeSupplierExtractJobHandler(
            new ExecutionIdentityAccessor(),
            extraction.Object,
            diagnostics,
            NullLogger<OutcomeSupplierExtractJobHandler>.Instance);
        var payload = new OutcomeSupplierExtractJobPayload(300001, null, 42, "bidops", 123, "提示词");
        var context = new BackgroundJobExecutionContext(new BackgroundJob
        {
            Id = 1,
            Payload = JsonSerializer.Serialize(payload, new JsonSerializerOptions(JsonSerializerDefaults.Web))
        });

        var result = await handler.HandleAsync(context);

        Assert.True(result.Succeeded);
        Assert.Equal(BackgroundJobResultStorageLimits.AiDiagnosticsMaxCharacters, result.MaxResultCharacters);
        using var document = JsonDocument.Parse(result.Result!);
        Assert.Equal(123, document.RootElement.GetProperty("rawNoticeId").GetInt64());
        Assert.Equal(3, document.RootElement.GetProperty("savedCount").GetInt32());
        Assert.Equal(1, document.RootElement.GetProperty("buyerCreatedCount").GetInt32());
        Assert.Equal(2, document.RootElement.GetProperty("supplierUpdatedCount").GetInt32());
        Assert.True(document.RootElement.GetProperty("reviewerPrompt").GetBoolean());
        var aiResponse = Assert.Single(document.RootElement.GetProperty("aiResponses").EnumerateArray());
        Assert.Equal("OutcomeSuppliers", aiResponse.GetProperty("use").GetString());
        var deepSeekResponse = Assert.Single(document.RootElement.GetProperty("deepSeekResponses").EnumerateArray());
        Assert.Equal("OutcomeSuppliers", deepSeekResponse.GetProperty("use").GetString());
        Assert.Equal("{\"records\":[]}", deepSeekResponse.GetProperty("assistantContent").GetString());
        Assert.Contains("已保存", result.Result);
        Assert.DoesNotContain("\\u", result.Result);
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
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.CrawlCheckpoint));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Opportunity));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Buyer));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.BuyerProcurement));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Supplier));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.SupplierEvidence));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.OutcomeSupplierRecord));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Matching));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.GoNoGoDecision));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.Pursuit));
        Assert.True(catalog.DataResources.ContainsKey(BidOpsDataResources.PursuitTask));
    }

    [Fact]
    public void StateGridEcpCrawlAdapter_DeclaresTableAndAttachmentCapabilities()
    {
        var adapter = new StateGridEcpCrawlAdapter();
        var source = new Atlas.Modules.BidOps.Entities.Crawling.CrawlSource
        {
            SourceType = BidOpsCrawlSourceTypes.StateGridEcp,
            BaseUrl = "https://ecp.sgcc.com.cn/ecp2.0/portal/",
            NeedLogin = false
        };

        Assert.True(adapter.CanHandle(source));
        Assert.True(adapter.SupportsInlineHtmlTables);
        Assert.True(adapter.SupportsAttachmentDiscovery);
        Assert.Contains("pdf", adapter.SupportedAttachmentTypes);
        Assert.Contains("zip", adapter.SupportedAttachmentTypes);
        Assert.True(adapter.CanImportDetail(new Uri("https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128522123684_2018060501171111")));
    }

    [Theory]
    [InlineData("项目单位：国网四川省电力公司", "国网四川省电力公司")]
    [InlineData("采购人: 北京电力交易中心有限公司", "北京电力交易中心有限公司")]
    [InlineData("中标人：许继电气股份有限公司", "许继电气股份有限公司")]
    public void BidOpsOrganizationNameNormalizer_RemovesCommonNoticeLabels(string value, string expected)
    {
        Assert.Equal(expected, BidOpsOrganizationNameNormalizer.Clean(value));
        Assert.Equal(expected, BidOpsOrganizationNameNormalizer.NormalizeForMatch(value));
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
        var crawlProgressRoute = typeof(BidOpsOperationsController)
            .GetMethod(nameof(BidOpsOperationsController.CrawlProgressAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var codexCliSettingsRoute = typeof(BidOpsOperationsController)
            .GetMethod(nameof(BidOpsOperationsController.UpdateCodexCliSettingsAsync))?
            .GetCustomAttributes<HttpPutAttribute>()
            .SingleOrDefault()?
            .Template;
        var codexCliScenarioSettingsRoute = typeof(BidOpsOperationsController)
            .GetMethod(nameof(BidOpsOperationsController.UpdateCodexCliScenarioSettingsAsync))?
            .GetCustomAttributes<HttpPutAttribute>()
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
        Assert.Equal("crawl-progress", crawlProgressRoute);
        Assert.Equal("ai-settings/codex-cli", codexCliSettingsRoute);
        Assert.Equal("ai-settings/codex-cli/scenario", codexCliScenarioSettingsRoute);
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
        Assert.NotNull(typeof(ReparseRawNoticeRequest).GetProperty(nameof(ReparseRawNoticeRequest.Prompt)));
        Assert.NotNull(typeof(AttachmentProcessJobPayload).GetProperty(nameof(AttachmentProcessJobPayload.ReviewerPrompt)));
        Assert.NotNull(typeof(StructuredParseJobPayload).GetProperty(nameof(StructuredParseJobPayload.ReviewerPrompt)));
    }

    [Fact]
    public void ReviewTasksController_DeclaresOutcomeAiReparseContract()
    {
        var route = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.OutcomeAiReparseAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var addRoute = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.AddOutcomeSupplierAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var updateRoute = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.UpdateOutcomeSupplierAsync))?
            .GetCustomAttributes<HttpPutAttribute>()
            .SingleOrDefault()?
            .Template;
        var deleteRoute = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.DeleteOutcomeSupplierAsync))?
            .GetCustomAttributes<HttpDeleteAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("{id:long}/outcome-ai-reparse", route);
        Assert.Equal("{id:long}/outcome-suppliers", addRoute);
        Assert.Equal("{id:long}/outcome-suppliers/{recordId:long}", updateRoute);
        Assert.Equal("{id:long}/outcome-suppliers/{recordId:long}", deleteRoute);
        Assert.NotNull(typeof(ReviewOutcomeAiReparseRequest).GetProperty(nameof(ReviewOutcomeAiReparseRequest.Prompt)));
        Assert.NotNull(typeof(ReviewOutcomeSupplierRecordEditRequest).GetProperty(nameof(ReviewOutcomeSupplierRecordEditRequest.SupplierName)));
        Assert.NotNull(typeof(ReviewOutcomeSupplierRecordEditRequest).GetProperty(nameof(ReviewOutcomeSupplierRecordEditRequest.AwardAmount)));
        Assert.NotNull(typeof(OutcomeSupplierRecordDto).GetProperty(nameof(OutcomeSupplierRecordDto.ExtractionOrder)));
        Assert.NotNull(typeof(OutcomeSupplierExtractJobPayload).GetProperty(nameof(OutcomeSupplierExtractJobPayload.ReviewerPrompt)));
    }

    [Fact]
    public void ReviewTasksController_DeclaresReviewAutomationContracts()
    {
        var bulkApproveRoute = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.BulkApproveAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var batchReparseRoute = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.BatchReparseAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var backfillRoute = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.EnqueueQualityBackfillAsync))?
            .GetCustomAttributes<HttpPostAttribute>()
            .SingleOrDefault()?
            .Template;
        var analysisRoute = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.GetCorrectionAnalysisAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;
        var metricsRoute = typeof(ReviewTasksController)
            .GetMethod(nameof(ReviewTasksController.GetEfficiencyMetricsAsync))?
            .GetCustomAttributes<HttpGetAttribute>()
            .SingleOrDefault()?
            .Template;

        Assert.Equal("bulk-approve", bulkApproveRoute);
        Assert.Equal("batch-reparse", batchReparseRoute);
        Assert.Equal("quality-backfill", backfillRoute);
        Assert.Equal("corrections/analysis", analysisRoute);
        Assert.Equal("efficiency-metrics", metricsRoute);
        Assert.NotNull(typeof(BulkApproveReviewTasksRequest).GetProperty(nameof(BulkApproveReviewTasksRequest.ReviewTaskIds)));
        Assert.NotNull(typeof(BatchReviewTaskReparseRequest).GetProperty(nameof(BatchReviewTaskReparseRequest.Prompt)));
        Assert.NotNull(typeof(ReviewQualityBackfillRequest).GetProperty(nameof(ReviewQualityBackfillRequest.DryRun)));
        Assert.NotNull(typeof(ReviewCorrectionAnalysisDto).GetProperty(nameof(ReviewCorrectionAnalysisDto.TopOriginalHeaders)));
        Assert.NotNull(typeof(ReviewEfficiencyMetricsDto).GetProperty(nameof(ReviewEfficiencyMetricsDto.LowRiskRatio)));
        Assert.NotNull(typeof(ReviewQualityBackfillJobPayload).GetProperty(nameof(ReviewQualityBackfillJobPayload.PauseSourceAware)));
    }

    [Fact]
    public void BidOpsNoticeListContracts_ExposeNoticeTypeFiltersAndUpdatedAt()
    {
        var noticeSearchParameter = typeof(NoticesController)
            .GetMethod(nameof(NoticesController.SearchAsync))?
            .GetParameters()
            .First()
            .ParameterType;

        Assert.Equal(typeof(NoticeSearchQuery), noticeSearchParameter);
        Assert.NotNull(typeof(ReviewTaskSearchQuery).GetProperty(nameof(ReviewTaskSearchQuery.NoticeType)));
        Assert.NotNull(typeof(ReviewTaskSearchQuery).GetProperty(nameof(ReviewTaskSearchQuery.ProjectCode)));
        Assert.NotNull(typeof(NoticeSearchQuery).GetProperty(nameof(NoticeSearchQuery.NoticeType)));
        Assert.NotNull(typeof(RawNoticeDto).GetProperty(nameof(RawNoticeDto.UpdatedAt)));
        Assert.NotNull(typeof(ReviewTaskDto).GetProperty(nameof(ReviewTaskDto.UpdatedAt)));
        Assert.NotNull(typeof(NoticeDto).GetProperty(nameof(NoticeDto.UpdatedAt)));
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
    public void BidOpsOutcomeSupplierTextParser_TreatsUnitlessOutcomeTableAmountsAsYuan()
    {
        const string text = """
国网浙江电力2026年第三次服务公开招标采购中标候选人名单公示
分标编号 包号 投标人名称 投标报价 评审得分
9001005-9999 包 01 杭州悦玛电力技术有限公司 112.325700 93.20
9001005-9999 包 02 山东大学 16.05 91.00
9001005-9999 包 03 北京乙科技有限公司 97.50% 88.00
""";

        var records = BidOpsOutcomeSupplierTextParser.Extract(
            "国网浙江电力2026年第三次服务公开招标采购中标候选人名单公示",
            "CandidateAnnouncement",
            text);

        Assert.Contains(records, x =>
            x.SupplierName == "杭州悦玛电力技术有限公司" &&
            x.AwardAmount == 112.33m);
        Assert.Contains(records, x =>
            x.SupplierName == "山东大学" &&
            x.AwardAmount == 16.05m);
        Assert.Contains(records, x =>
            x.SupplierName == "北京乙科技有限公司" &&
            x.AwardAmount == null);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractBuilder_ExtractsWrappedPdfCandidateRows()
    {
        const string text = """
国网浙江电力 2026 年第三次服务公开招标采购中标候选人名单公示及否决原因公示
（招标编号：ZBGW26-008）
推荐中标候选人
序
号 分标编号 分标名
称 包号 推荐中标人
投标总价
(万元人民
币)/投标折
扣率
1 112610-900
1002-0001
电网工
程施工-
基建
包 1 浙江省送变电工程有
限公司
10452.4907
00
满足招
标文件
要求
综合排
序第一
名
2 112610-900
1002-0001
电网工
程施工-
基建
包 2 浙江大有实业有限公
司
2872.41300
0
满足招
标文件
要求
综合排
序第一
名
否决原因公示
""";

        var records = BidOpsOutcomeSupplierExtractBuilder.Extract(
            "国网浙江电力2026年第三次服务公开招标采购中标候选人名单公示及否决原因公示",
            "CandidateAnnouncement",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128520069185_2018060501171107",
            new DateTime(2026, 6, 12, 13, 52, 44, DateTimeKind.Utc),
            text,
            323981982046490624);

        Assert.Equal(2, records.Count);
        Assert.All(records, x => Assert.Equal(BidOpsOutcomeTypes.Candidate, x.OutcomeType));
        Assert.Contains(records, x =>
            x.SupplierName == "浙江省送变电工程有限公司" &&
            x.LotNo == "112610-9001002-0001" &&
            x.LotName == "电网工程施工-基建" &&
            x.PackageNo == "包 1" &&
            x.Rank == 1 &&
            x.AwardAmount == 104524907.00m);
        Assert.Contains(records, x =>
            x.SupplierName == "浙江大有实业有限公司" &&
            x.PackageNo == "包 2" &&
            x.AwardAmount == 28724130.00m);
    }

    [Fact]
    public void BidOpsOutcomeSupplierExtractBuilder_ExtractsWrappedPdfAwardRows()
    {
        const string text = """
国网测试项目成交结果公告
采购编号：SGCC-RESULT-001
序号 分标编号 分标名称 包号 成交人 成交金额（万元）
1 9003001-9999
综合服务
包 2 北京乙科技有
限公司
123.4500
采购人：国网测试单位
""";

        var record = Assert.Single(BidOpsOutcomeSupplierExtractBuilder.Extract(
            "国网测试项目成交结果公告",
            "AwardAnnouncement",
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/test",
            null,
            text,
            1));

        Assert.Equal(BidOpsOutcomeTypes.Awarded, record.OutcomeType);
        Assert.Equal("北京乙科技有限公司", record.SupplierName);
        Assert.Equal("9003001-9999", record.LotNo);
        Assert.Equal("综合服务", record.LotName);
        Assert.Equal("包 2", record.PackageNo);
        Assert.Equal(1234500.00m, record.AwardAmount);
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
        var page = StateGridEcpWcmParser.ParseNoticeListPage(
            listJson.Replace("\"noteList\"", "\"total\": 123, \"noteList\"", StringComparison.Ordinal),
            "https://ecp.sgcc.com.cn/ecp2.0/portal/",
            10);

        var notice = Assert.Single(notices);
        Assert.Equal(123, page.TotalCount);
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
    public void StateGridEcpWcmParser_PreservesWinHtmlTablesForOutcomeExtraction()
    {
        const string detailJson = """
{
  "successful": true,
  "resultValue": {
    "fileFlag": "0",
    "notice": {
      "PURPRJ_NAME": "国家电网有限公司2026年特高压项目第二次服务公开招标采购中标公告",
      "CONT": "<p align=\"center\"><b>中标公告</b></p><p><b>（招标编号：</b><b>0711-26OTL04213025</b><b>）</b></p><table border=\"1\"><tbody><tr><td><p><b><span>分标编号</span></b></p></td><td><p><b><span>分标名称</span></b></p></td><td><p><b><span>包号</span></b></p></td><td><p><b><span>中标状态</span></b></p></td><td><p><b><span>项目单位</span></b></p></td><td><p><b><span>中标人</span></b></p></td></tr><tr><td><p><span>SG2674-9001-13028</span></p></td><td><p><span>变电站土建施工</span></p></td><td><p><span><span>包</span><span>1</span></span></p></td><td><p><span>中标</span></p></td><td><p><span>国网四川省电力公司</span></p></td><td><p><span>中国电建集团江西省水电工程局有限公司</span></p></td></tr></tbody></table>"
    }
  }
}
""";
        var notice = new StateGridEcpApiNotice(
            string.Empty,
            "https://ecp.sgcc.com.cn/ecp2.0/portal/#/doc/doci-win/2606128522123684_2018060501171111",
            "doci-win",
            "2018060501171111",
            2606128522123684,
            null,
            null,
            string.Empty,
            string.Empty);

        var document = StateGridEcpWcmParser.ParseNoticeDetail(detailJson, notice);
        Assert.Contains("<table", document.Html, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("分标编号", document.Html);
        Assert.Contains("中国电建集团江西省水电工程局有限公司", document.Html);

        var records = BidOpsAwardEvidenceParser.Extract([
            new BidOpsEvidenceDocument(
                new EvidenceSourceRef(1, null, "AwardAnnouncement", notice.DetailUrl, null, null, null, null, null),
                document.Title,
                "AwardAnnouncement",
                document.PublishTime,
                document.Text)
        ]);

        var record = Assert.Single(records);
        Assert.Equal("0711-26OTL04213025", record.ProjectCode);
        Assert.Equal("SG2674-9001-13028", record.LotNo);
        Assert.Equal("变电站土建施工", record.LotName);
        Assert.Equal("包1", record.PackageNo);
        Assert.Equal("国网四川省电力公司", record.ProjectUnit);
        Assert.Equal("中国电建集团江西省水电工程局有限公司", record.AwardedSupplierName);
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
        Assert.Contains("| 包件号 | 技术规范 | 数量 | 最高限价 |", text);
        Assert.Contains("包件号", text);
        Assert.Contains("10kV环网柜", text);
        Assert.Contains("电缆附件", text);
        Assert.Contains("300000", text);
    }

    [Fact]
    public async Task BidOpsTextExtractor_ExtractsGbkZipEntryNames()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

        var extractor = new BidOpsTextExtractor();
        await using var xlsx = CreateXlsx(
            "需求明细",
            [
                ["分标编号", "包名称", "最高限价(万元)（含税）"],
                ["19FBAC-9013001-3000", "包1房屋维修", "45.78"]
            ]);
        await using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true, Encoding.GetEncoding("GB18030")))
        {
            AddZipEntry(archive, "附件/采购公告附件.xlsx", xlsx.ToArray());
        }

        stream.Position = 0;
        var text = await extractor.ExtractAsync(stream, "公告附件.zip", "application/zip");

        Assert.Contains("File: 附件/采购公告附件.xlsx", text);
        Assert.Contains("| 分标编号 | 包名称 | 最高限价(万元)（含税） |", text);
        Assert.Contains("45.78", text);
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
    public void BidOpsEcpProcurementTableParser_ParsesEmbeddedPackageNoAndMaxPrice()
    {
        const string text = """
国网测试公告
resultValue.notice.PURPRJ_CODE: 19FBAC
## 表格 1：Sheet: 附件1项目概况与资质业绩要求
| 采购编号 | 分标编号 | 分标名称 | 包号 | 包名称 | 子项目名称 | 行报价最高限价（含税/万元） | 子项最高限价(万元)（含税） | 报价方式 |
|---|---|---|---|---|---|---|---|---|
| 19FBAC | 19FBAC-9013001-3000 | 房屋维修-施工 | 包1 | 包1房屋维修施工 | 国网四川巴中项目 | 45.78 | 45.78 | 固定总价 |
|  |  |  |  |  | 国网四川通江项目 |  | 12.34 | 固定总价 |
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        var package = Assert.Single(extract.Packages);
        Assert.Equal("19FBAC-9013001-3000", package.LotNo);
        Assert.Equal("房屋维修-施工", package.LotName);
        Assert.Equal("包1", package.PackageNo);
        Assert.Equal("包1房屋维修施工", package.PackageName);
        Assert.Equal(457800m, package.MaxPrice);
    }

    [Fact]
    public void BidOpsEcpProcurementTableParser_NormalizesMoneyHeaderAliasesAndSkipsRateColumns()
    {
        const string text = """
国网测试公告
## 表格 1：Sheet: 采购金额
| 采购编号 | 分标编号 | 分标名称 | 包号 | 包名称 | 采购金额（元） | 最高限价（%） |
|---|---|---|---|---|---|---|
| 19FBAC | 19FBAC-9012002-9999 | 技术服务 | 包1 | 包1技术服务 | 123456 | 95 |

## 表格 2：Sheet: 包估算与限价
| 采购编号 | 分标编号 | 分标名称 | 包号 | 包名称 | 包估算金额（万元） | 最高应答限价含税（元或折扣比例) | 行报价最高限价对应税率（%） |
|---|---|---|---|---|---|---|---|
| 19FBAC | 19FBAC-9013001-3000 | 房屋维修-施工 | 包2 | 包2房屋维修施工 | 45.78 | 190000 | 9 |
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        Assert.Equal(2, extract.Packages.Count);
        Assert.Equal(123456m, extract.Packages[0].BudgetAmount);
        Assert.Null(extract.Packages[0].MaxPrice);
        Assert.Equal(457800m, extract.Packages[1].BudgetAmount);
        Assert.Equal(190000m, extract.Packages[1].MaxPrice);
    }

    [Fact]
    public void BidOpsEcpProcurementTableParser_ParsesDemandListRequirementsWithoutPackageNoColumn()
    {
        const string text = """
国网测试公告
resultValue.notice.PURPRJ_CODE: 20F541
## 表格 1：Sheet: 采购公告附件
| 序号 | 项目单位 | 分标编号 | 分标名称 | 包名称 | 项目名称 | 网省采购申请编号 | 项目概况 | 资格条件（资质要求） | 资格条件（业绩要求） | 资格条件（人员要求） | 资格条件（其他） | 工期/服务期 | 实施地点 | 项目类型 |
|---|---|---|---|---|---|---|---|---|---|---|---|---|---|---|
| 1 | 市北供电分公司 | 20F541-9012002-9999 | 技术服务 | 包1电网咨询服务 | 配网咨询服务 | 100000001 | 完成配网咨询服务。 | 具有工程咨询资信证书 | 近三年具有类似业绩 | 项目负责人具备中级职称 | 不接受联合体 | 自合同签订之日起90日 | 重庆 | 服务 |
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        var package = Assert.Single(extract.Packages);
        Assert.Equal("20F541-9012002-9999", package.LotNo);
        Assert.Equal("技术服务", package.LotName);
        Assert.Equal("包1", package.PackageNo);
        Assert.Equal("包1电网咨询服务", package.PackageName);
        Assert.Null(package.MaxPrice);
        Assert.Contains(package.Requirements, x => x.RequirementType == "Qualification" && x.OriginalText.Contains("工程咨询资信证书", StringComparison.Ordinal));
        Assert.Contains(package.Requirements, x => x.RequirementType == "Performance");
        Assert.Contains(package.Requirements, x => x.RequirementType == "Personnel");
    }

    [Fact]
    public void BidOpsEcpProcurementTableParser_PreservesScopeTableOrderAcrossMultipleAttachments()
    {
        const string text = """
国网测试公告
## 表格 1：Sheet: 服务
| 分标编号 | 分标名称 | 包号 | 包名称 | 最高限价(万元) |
|---|---|---|---|---|
| 19FBAC-9012002-9999 | 技术服务 | 包1 | 包1技术服务 | 18.2495 |

## 表格 2：Sheet: 施工
| 分标编号 | 分标名称 | 包号 | 包名称 | 最高限价(万元) |
|---|---|---|---|---|
| 19FBAC-9013001-3000 | 房屋维修-施工 | 包1 | 包1房屋维修 | 45.78 |
""";

        var extract = BidOpsDeterministicNoticeParser.Extract("国网测试公告", text);

        Assert.Equal(2, extract.Packages.Count);
        Assert.Equal("技术服务", extract.Packages[0].LotName);
        Assert.Equal("房屋维修-施工", extract.Packages[1].LotName);
        Assert.Equal(182495m, extract.Packages[0].MaxPrice);
        Assert.Equal(457800m, extract.Packages[1].MaxPrice);
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

    private sealed class StubHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> _sendAsync;

        public StubHttpMessageHandler(Func<HttpRequestMessage, CancellationToken, Task<HttpResponseMessage>> sendAsync)
        {
            _sendAsync = sendAsync;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request,
            CancellationToken cancellationToken)
        {
            return _sendAsync(request, cancellationToken);
        }
    }

    private sealed class FakeCodexCliClient : IBidOpsCodexCliClient
    {
        private readonly BidOpsCodexCliResult _result;

        public FakeCodexCliClient(BidOpsCodexCliResult result)
        {
            _result = result;
        }

        public List<BidOpsCodexCliRequest> Requests { get; } = [];

        public Task<BidOpsCodexCliResult> ExecuteJsonAsync(
            BidOpsCodexCliRequest request,
            CancellationToken ct = default)
        {
            Requests.Add(request);
            return Task.FromResult(_result);
        }
    }

    private sealed class ThrowingCodexCliClient : IBidOpsCodexCliClient
    {
        private readonly Exception _exception;

        public ThrowingCodexCliClient(Exception exception)
        {
            _exception = exception;
        }

        public Task<BidOpsCodexCliResult> ExecuteJsonAsync(
            BidOpsCodexCliRequest request,
            CancellationToken ct = default)
        {
            throw _exception;
        }
    }

    private sealed class CapturingLogger<T> : ILogger<T>
    {
        public List<string> Messages { get; } = [];

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return NullScope.Instance;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return true;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            Messages.Add(formatter(state, exception));
        }

        private sealed class NullScope : IDisposable
        {
            public static readonly NullScope Instance = new();

            public void Dispose()
            {
            }
        }
    }

    private sealed record StateGridPackageRow(
        string LotCode,
        string LotName,
        string PackageNo,
        string PackageName,
        string Scope,
        string Period);
}
