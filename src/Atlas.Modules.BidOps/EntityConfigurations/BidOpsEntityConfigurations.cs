using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Buyers;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Matching;
using Atlas.Modules.BidOps.Entities.Opportunities;
using Atlas.Modules.BidOps.Entities.Outcomes;
using Atlas.Modules.BidOps.Entities.Pursuits;
using Atlas.Modules.BidOps.Entities.Staging;
using Atlas.Modules.BidOps.Entities.Suppliers;
using Atlas.Modules.BidOps.Entities.Tendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Modules.BidOps.EntityConfigurations;

internal static class BidOpsConfigurationHelpers
{
    public static void ConfigureTenantEntity<TEntity>(this EntityTypeBuilder<TEntity> builder)
        where TEntity : BidOpsTenantEntity
    {
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt });
    }
}

public sealed class BidOpsRuntimeSettingConfiguration : IEntityTypeConfiguration<BidOpsRuntimeSetting>
{
    public void Configure(EntityTypeBuilder<BidOpsRuntimeSetting> builder)
    {
        builder.ToTable("bidops_runtime_setting");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.SettingKey).HasColumnType("varchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(x => x.SettingValue).HasColumnType("varchar(512)").HasMaxLength(512);
        builder.Property(x => x.UpdatedByUserName).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.HasIndex(x => new { x.TenantId, x.SettingKey }).IsUnique();
    }
}

public sealed class CrawlSourceConfiguration : IEntityTypeConfiguration<CrawlSource>
{
    public void Configure(EntityTypeBuilder<CrawlSource> builder)
    {
        builder.ToTable("bidops_crawl_source");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.SourceType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.BaseUrl).HasMaxLength(1000);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.RobotsPolicyNote).HasMaxLength(1000);
        builder.Property(x => x.PauseReason).HasMaxLength(1000);
        builder.Property(x => x.Remark).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Enabled, x.Priority });
    }
}

public sealed class CrawlChannelConfiguration : IEntityTypeConfiguration<CrawlChannel>
{
    public void Configure(EntityTypeBuilder<CrawlChannel> builder)
    {
        builder.ToTable("bidops_crawl_channel");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Code).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasMaxLength(200).IsRequired();
        builder.Property(x => x.NoticeType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ListUrl).HasMaxLength(1000);
        builder.Property(x => x.Region).HasMaxLength(128);
        builder.Property(x => x.Industry).HasMaxLength(128);
        builder.Property(x => x.ScheduleMode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.DailyScanTime).HasMaxLength(16);
        builder.Property(x => x.ListItemSelector).HasMaxLength(500);
        builder.Property(x => x.TitleSelector).HasMaxLength(500);
        builder.Property(x => x.UrlSelector).HasMaxLength(500);
        builder.Property(x => x.PublishTimeSelector).HasMaxLength(500);
        builder.Property(x => x.DetailContentSelector).HasMaxLength(500);
        builder.Property(x => x.AttachmentSelector).HasMaxLength(500);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.SourceId, x.Code }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Enabled, x.LastScanTime });
    }
}

public sealed class RawNoticeConfiguration : IEntityTypeConfiguration<RawNotice>
{
    public void Configure(EntityTypeBuilder<RawNotice> builder)
    {
        builder.ToTable("bidops_raw_notice");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.SourceNoticeId).HasMaxLength(128);
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.DetailUrl).HasMaxLength(1500).IsRequired();
        builder.Property(x => x.DetailUrlHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.NoticeType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(x => x.StorageProvider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.HtmlSnapshotStorageKey).HasMaxLength(1000);
        builder.Property(x => x.TextContentStorageKey).HasMaxLength(1000);
        builder.Property(x => x.TextPreview).HasMaxLength(4000);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.SourceId, x.DetailUrlHash }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.NoticeType, x.SourceNoticeId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.FetchTime });
        builder.HasIndex(x => new { x.TenantId, x.ContentHash });
    }
}

public sealed class RawAttachmentConfiguration : IEntityTypeConfiguration<RawAttachment>
{
    public void Configure(EntityTypeBuilder<RawAttachment> builder)
    {
        builder.ToTable("bidops_raw_attachment");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.FileName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.FileUrl).HasMaxLength(1500);
        builder.Property(x => x.FileType).HasMaxLength(64);
        builder.Property(x => x.FileHash).HasMaxLength(64);
        builder.Property(x => x.StorageProvider).HasMaxLength(32).IsRequired();
        builder.Property(x => x.StorageKey).HasMaxLength(1000);
        builder.Property(x => x.TextContentStorageKey).HasMaxLength(1000);
        builder.Property(x => x.DownloadStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.TextExtractStatus).HasConversion<int>().IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId, x.FileHash }).IsUnique();
    }
}

public sealed class CrawlRunLogConfiguration : IEntityTypeConfiguration<CrawlRunLog>
{
    public void Configure(EntityTypeBuilder<CrawlRunLog> builder)
    {
        builder.ToTable("bidops_crawl_run_log");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Operation).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
        builder.Property(x => x.Message).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.SourceId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.BackgroundJobId });
    }
}

public sealed class CrawlCheckpointConfiguration : IEntityTypeConfiguration<CrawlCheckpoint>
{
    public void Configure(EntityTypeBuilder<CrawlCheckpoint> builder)
    {
        builder.ToTable("bidops_crawl_checkpoint");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Mode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.CursorKind).HasMaxLength(32).IsRequired();
        builder.Property(x => x.NextCursor).HasMaxLength(128);
        builder.Property(x => x.LastSuccessfulCursor).HasMaxLength(128);
        builder.Property(x => x.PauseReason).HasMaxLength(1000);
        builder.Property(x => x.LastError).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.ChannelId, x.Mode }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.SourceId, x.Status, x.LastRunAt });
    }
}

public sealed class CrawlRunConfiguration : IEntityTypeConfiguration<CrawlRun>
{
    public void Configure(EntityTypeBuilder<CrawlRun> builder)
    {
        builder.ToTable("bidops_crawl_run");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Mode).HasMaxLength(32).IsRequired();
        builder.Property(x => x.Status).HasMaxLength(32).IsRequired();
        builder.Property(x => x.StartCursor).HasMaxLength(128);
        builder.Property(x => x.EndCursor).HasMaxLength(128);
        builder.Property(x => x.Message).HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.ChannelId, x.StartedAt });
        builder.HasIndex(x => new { x.TenantId, x.CheckpointId, x.StartedAt });
        builder.HasIndex(x => new { x.TenantId, x.BackgroundJobId });
    }
}

public sealed class NoticeStagingConfiguration : IEntityTypeConfiguration<NoticeStaging>
{
    public void Configure(EntityTypeBuilder<NoticeStaging> builder)
    {
        builder.ToTable("bidops_notice_staging");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.NoticeType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProjectName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ProjectCode).HasMaxLength(128);
        builder.Property(x => x.BuyerName).HasMaxLength(300);
        builder.Property(x => x.AgencyName).HasMaxLength(300);
        builder.Property(x => x.Region).HasMaxLength(128);
        builder.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.AiConfidence).HasColumnType("decimal(5,4)");
        builder.Property(x => x.ReviewStatus).HasConversion<int>().IsRequired();
        builder.Property(x => x.RawAiOutputStorageKey).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ReviewStatus, x.CreatedAt });
    }
}

public sealed class PackageStagingConfiguration : IEntityTypeConfiguration<PackageStaging>
{
    public void Configure(EntityTypeBuilder<PackageStaging> builder)
    {
        builder.ToTable("bidops_package_staging");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.LotNo).HasMaxLength(128);
        builder.Property(x => x.LotName).HasMaxLength(300);
        builder.Property(x => x.PackageNo).HasMaxLength(128);
        builder.Property(x => x.PackageName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(128);
        builder.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(x => x.Unit).HasMaxLength(64);
        builder.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaxPrice).HasColumnType("decimal(18,2)");
        builder.Property(x => x.DeliveryPlace).HasMaxLength(300);
        builder.Property(x => x.DeliveryPeriod).HasMaxLength(200);
        builder.Property(x => x.AiConfidence).HasColumnType("decimal(5,4)");
        builder.Property(x => x.ReviewStatus).HasConversion<int>().IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.NoticeStagingId });
    }
}

public sealed class RequirementStagingConfiguration : IEntityTypeConfiguration<RequirementStaging>
{
    public void Configure(EntityTypeBuilder<RequirementStaging> builder)
    {
        builder.ToTable("bidops_requirement_staging");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.RequirementType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OriginalText).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RequiredEvidenceType).HasMaxLength(128);
        builder.Property(x => x.RiskLevel).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AiExplanation).HasMaxLength(1000);
        builder.Property(x => x.AiConfidence).HasColumnType("decimal(5,4)");
        builder.Property(x => x.ReviewStatus).HasConversion<int>().IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.PackageStagingId });
        builder.HasIndex(x => new { x.TenantId, x.RiskLevel });
    }
}

public sealed class ProcurementDetailStagingConfiguration : IEntityTypeConfiguration<ProcurementDetailStaging>
{
    public void Configure(EntityTypeBuilder<ProcurementDetailStaging> builder)
    {
        builder.ToTable("bidops_procurement_detail_staging");
        builder.ConfigureTenantEntity();
        ConfigureProcurementDetailShape(builder);
        builder.Property(x => x.AiConfidence).HasColumnType("decimal(5,4)");
        builder.Property(x => x.ReviewStatus).HasConversion<int>().IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.NoticeStagingId });
        builder.HasIndex(x => new { x.TenantId, x.PackageStagingId });
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId, x.RawAttachmentId, x.TableIndex, x.RowIndex });
        builder.HasIndex(x => new { x.TenantId, x.ProjectCode, x.LotNo, x.PackageNo });
        builder.HasIndex(x => new { x.TenantId, x.ReviewStatus, x.CreatedAt });
    }

    private static void ConfigureProcurementDetailShape(EntityTypeBuilder<ProcurementDetailStaging> builder)
    {
        builder.Property(x => x.SourceSheetName).HasMaxLength(300);
        builder.Property(x => x.ProjectCode).HasMaxLength(128);
        builder.Property(x => x.ProjectName).HasMaxLength(500);
        builder.Property(x => x.ProcurementApplicationNo).HasMaxLength(128);
        builder.Property(x => x.LineItemNo).HasMaxLength(128);
        builder.Property(x => x.MaterialCode).HasMaxLength(128);
        builder.Property(x => x.LotSequence).HasMaxLength(64);
        builder.Property(x => x.LotNo).HasMaxLength(128);
        builder.Property(x => x.LotName).HasMaxLength(300);
        builder.Property(x => x.EcpLotName).HasMaxLength(300);
        builder.Property(x => x.PackageNo).HasMaxLength(128);
        builder.Property(x => x.PackageName).HasMaxLength(500);
        builder.Property(x => x.PackageType).HasMaxLength(128);
        builder.Property(x => x.Category).HasMaxLength(128);
        builder.Property(x => x.ProcurementMethod).HasMaxLength(128);
        builder.Property(x => x.BuyerName).HasMaxLength(300);
        builder.Property(x => x.ProjectUnit).HasMaxLength(300);
        builder.Property(x => x.ConstructionUnit).HasMaxLength(300);
        builder.Property(x => x.ProcurementContent).HasColumnType("text");
        builder.Property(x => x.ScopeText).HasColumnType("text");
        builder.Property(x => x.ProjectOverview).HasColumnType("text");
        builder.Property(x => x.Location).HasMaxLength(500);
        builder.Property(x => x.VoltageLevel).HasMaxLength(128);
        builder.Property(x => x.ProcurementAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ItemEstimatedAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PackageEstimatedAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaxPrice).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaxPriceRatePercent).HasColumnType("decimal(9,4)");
        builder.Property(x => x.TaxRatePercent).HasColumnType("decimal(9,4)");
        builder.Property(x => x.ResponseGuaranteeAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.QuoteMode).HasMaxLength(128);
        builder.Property(x => x.SettlementMode).HasMaxLength(128);
        builder.Property(x => x.ServicePeriodText).HasMaxLength(300);
        builder.Property(x => x.QualificationRequirement).HasColumnType("text");
        builder.Property(x => x.PerformanceRequirement).HasColumnType("text");
        builder.Property(x => x.PersonnelRequirement).HasColumnType("text");
        builder.Property(x => x.OtherRequirement).HasColumnType("text");
        builder.Property(x => x.JointVentureAllowed).HasMaxLength(128);
        builder.Property(x => x.SubcontractAllowed).HasMaxLength(128);
        builder.Property(x => x.AwardLimit).HasMaxLength(500);
        builder.Property(x => x.TechnicalSpecId).HasMaxLength(256);
        builder.Property(x => x.ContractTemplate).HasMaxLength(500);
        builder.Property(x => x.BusinessWeight).HasColumnType("decimal(5,2)");
        builder.Property(x => x.TechnicalWeight).HasColumnType("decimal(5,2)");
        builder.Property(x => x.PriceWeight).HasColumnType("decimal(5,2)");
        builder.Property(x => x.PriceCalculationMethod).HasMaxLength(300);
        builder.Property(x => x.PriceParameter).HasMaxLength(500);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.OriginalHeaderJson).HasColumnType("longtext");
        builder.Property(x => x.OriginalRowJson).HasColumnType("longtext");
        builder.Property(x => x.NormalizedFieldsJson).HasColumnType("longtext");
    }
}

public sealed class ReviewTaskConfiguration : IEntityTypeConfiguration<ReviewTask>
{
    public void Configure(EntityTypeBuilder<ReviewTask> builder)
    {
        builder.ToTable("bidops_review_task");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.BizType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TaskTitle).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.QualityScore).IsRequired();
        builder.Property(x => x.RiskLevel).HasConversion<int>().IsRequired();
        builder.Property(x => x.QualityIssueCount).IsRequired();
        builder.Property(x => x.HighRiskIssueCount).IsRequired();
        builder.Property(x => x.ReviewRecommendation).HasConversion<int>().IsRequired();
        builder.Property(x => x.Decision).HasMaxLength(128);
        builder.Property(x => x.Remark).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.BizType, x.BizId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.Priority, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.RiskLevel, x.Status, x.CreatedAt });
    }
}

public sealed class ReviewQualityIssueConfiguration : IEntityTypeConfiguration<ReviewQualityIssue>
{
    public void Configure(EntityTypeBuilder<ReviewQualityIssue> builder)
    {
        builder.ToTable("bidops_review_quality_issue");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.IssueType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.Severity).HasConversion<int>().IsRequired();
        builder.Property(x => x.FieldName).HasMaxLength(128);
        builder.Property(x => x.Message).HasMaxLength(1000).IsRequired();
        builder.Property(x => x.EvidenceJson).HasColumnType("longtext");
        builder.HasIndex(x => new { x.TenantId, x.ReviewTaskId, x.IsResolved });
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId });
        builder.HasIndex(x => new { x.TenantId, x.NoticeStagingId });
        builder.HasIndex(x => new { x.TenantId, x.Severity, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.IssueType });
    }
}

public sealed class ReviewCorrectionSampleConfiguration : IEntityTypeConfiguration<ReviewCorrectionSample>
{
    public void Configure(EntityTypeBuilder<ReviewCorrectionSample> builder)
    {
        builder.ToTable("bidops_review_correction_sample");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.NoticeType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceKind).HasMaxLength(64).IsRequired();
        builder.Property(x => x.FieldName).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OriginalValue).HasColumnType("longtext");
        builder.Property(x => x.CorrectedValue).HasColumnType("longtext");
        builder.Property(x => x.OriginalHeader).HasMaxLength(300);
        builder.Property(x => x.OriginalRowJson).HasColumnType("longtext");
        builder.Property(x => x.ReviewerPrompt).HasColumnType("longtext");
        builder.Property(x => x.Reason).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.ReviewTaskId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.SourceKind, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.FieldName, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.NoticeType, x.CreatedAt });
    }
}

public sealed class NoticeConfiguration : IEntityTypeConfiguration<Notice>
{
    public void Configure(EntityTypeBuilder<Notice> builder)
    {
        builder.ToTable("bidops_notice");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Title).HasMaxLength(500).IsRequired();
        builder.Property(x => x.NoticeType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProjectName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.ProjectCode).HasMaxLength(128);
        builder.Property(x => x.BuyerName).HasMaxLength(300);
        builder.Property(x => x.AgencyName).HasMaxLength(300);
        builder.Property(x => x.Region).HasMaxLength(128);
        builder.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProjectCode });
        builder.HasIndex(x => new { x.TenantId, x.PublishTime });
    }
}

public sealed class TenderPackageConfiguration : IEntityTypeConfiguration<TenderPackage>
{
    public void Configure(EntityTypeBuilder<TenderPackage> builder)
    {
        builder.ToTable("bidops_tender_package");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.LotNo).HasMaxLength(128);
        builder.Property(x => x.LotName).HasMaxLength(300);
        builder.Property(x => x.PackageNo).HasMaxLength(128);
        builder.Property(x => x.PackageName).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Category).HasMaxLength(128);
        builder.Property(x => x.Quantity).HasColumnType("decimal(18,4)");
        builder.Property(x => x.Unit).HasMaxLength(64);
        builder.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaxPrice).HasColumnType("decimal(18,2)");
        builder.Property(x => x.DeliveryPlace).HasMaxLength(300);
        builder.Property(x => x.DeliveryPeriod).HasMaxLength(200);
        builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.NoticeId });
        builder.HasIndex(x => new { x.TenantId, x.NoticeId, x.PackageNo });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}

public sealed class RequirementItemConfiguration : IEntityTypeConfiguration<RequirementItem>
{
    public void Configure(EntityTypeBuilder<RequirementItem> builder)
    {
        builder.ToTable("bidops_requirement_item");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.RequirementType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.OriginalText).HasMaxLength(2000).IsRequired();
        builder.Property(x => x.RequiredEvidenceType).HasMaxLength(128);
        builder.Property(x => x.RiskLevel).HasMaxLength(64).IsRequired();
        builder.Property(x => x.AiExplanation).HasMaxLength(1000);
        builder.Property(x => x.ManualRemark).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.PackageId });
        builder.HasIndex(x => new { x.TenantId, x.RiskLevel });
    }
}

public sealed class ProcurementDetailConfiguration : IEntityTypeConfiguration<ProcurementDetail>
{
    public void Configure(EntityTypeBuilder<ProcurementDetail> builder)
    {
        builder.ToTable("bidops_procurement_detail");
        builder.ConfigureTenantEntity();
        ConfigureProcurementDetailShape(builder);
        builder.Property(x => x.Status).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.NoticeId });
        builder.HasIndex(x => new { x.TenantId, x.TenderPackageId });
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId, x.RawAttachmentId, x.TableIndex, x.RowIndex });
        builder.HasIndex(x => new { x.TenantId, x.ProjectCode, x.LotNo, x.PackageNo });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }

    private static void ConfigureProcurementDetailShape(EntityTypeBuilder<ProcurementDetail> builder)
    {
        builder.Property(x => x.SourceSheetName).HasMaxLength(300);
        builder.Property(x => x.ProjectCode).HasMaxLength(128);
        builder.Property(x => x.ProjectName).HasMaxLength(500);
        builder.Property(x => x.ProcurementApplicationNo).HasMaxLength(128);
        builder.Property(x => x.LineItemNo).HasMaxLength(128);
        builder.Property(x => x.MaterialCode).HasMaxLength(128);
        builder.Property(x => x.LotSequence).HasMaxLength(64);
        builder.Property(x => x.LotNo).HasMaxLength(128);
        builder.Property(x => x.LotName).HasMaxLength(300);
        builder.Property(x => x.EcpLotName).HasMaxLength(300);
        builder.Property(x => x.PackageNo).HasMaxLength(128);
        builder.Property(x => x.PackageName).HasMaxLength(500);
        builder.Property(x => x.PackageType).HasMaxLength(128);
        builder.Property(x => x.Category).HasMaxLength(128);
        builder.Property(x => x.ProcurementMethod).HasMaxLength(128);
        builder.Property(x => x.BuyerName).HasMaxLength(300);
        builder.Property(x => x.ProjectUnit).HasMaxLength(300);
        builder.Property(x => x.ConstructionUnit).HasMaxLength(300);
        builder.Property(x => x.ProcurementContent).HasColumnType("text");
        builder.Property(x => x.ScopeText).HasColumnType("text");
        builder.Property(x => x.ProjectOverview).HasColumnType("text");
        builder.Property(x => x.Location).HasMaxLength(500);
        builder.Property(x => x.VoltageLevel).HasMaxLength(128);
        builder.Property(x => x.ProcurementAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.BudgetAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.ItemEstimatedAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.PackageEstimatedAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaxPrice).HasColumnType("decimal(18,2)");
        builder.Property(x => x.MaxPriceRatePercent).HasColumnType("decimal(9,4)");
        builder.Property(x => x.TaxRatePercent).HasColumnType("decimal(9,4)");
        builder.Property(x => x.ResponseGuaranteeAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.QuoteMode).HasMaxLength(128);
        builder.Property(x => x.SettlementMode).HasMaxLength(128);
        builder.Property(x => x.ServicePeriodText).HasMaxLength(300);
        builder.Property(x => x.QualificationRequirement).HasColumnType("text");
        builder.Property(x => x.PerformanceRequirement).HasColumnType("text");
        builder.Property(x => x.PersonnelRequirement).HasColumnType("text");
        builder.Property(x => x.OtherRequirement).HasColumnType("text");
        builder.Property(x => x.JointVentureAllowed).HasMaxLength(128);
        builder.Property(x => x.SubcontractAllowed).HasMaxLength(128);
        builder.Property(x => x.AwardLimit).HasMaxLength(500);
        builder.Property(x => x.TechnicalSpecId).HasMaxLength(256);
        builder.Property(x => x.ContractTemplate).HasMaxLength(500);
        builder.Property(x => x.BusinessWeight).HasColumnType("decimal(5,2)");
        builder.Property(x => x.TechnicalWeight).HasColumnType("decimal(5,2)");
        builder.Property(x => x.PriceWeight).HasColumnType("decimal(5,2)");
        builder.Property(x => x.PriceCalculationMethod).HasMaxLength(300);
        builder.Property(x => x.PriceParameter).HasMaxLength(500);
        builder.Property(x => x.Remarks).HasMaxLength(1000);
        builder.Property(x => x.OriginalHeaderJson).HasColumnType("longtext");
        builder.Property(x => x.OriginalRowJson).HasColumnType("longtext");
        builder.Property(x => x.NormalizedFieldsJson).HasColumnType("longtext");
    }
}

public sealed class OpportunityConfiguration : IEntityTypeConfiguration<Opportunity>
{
    public void Configure(EntityTypeBuilder<Opportunity> builder)
    {
        builder.ToTable("bidops_opportunity");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.OpportunityNo).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
        builder.Property(x => x.Stage).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActiveMarker).HasColumnType("varchar(16)").HasMaxLength(16);
        builder.Property(x => x.EstimatedAmount).HasPrecision(18, 2);
        builder.Property(x => x.ValueScore).HasPrecision(6, 2);
        builder.Property(x => x.ValueLevel).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Decision).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.AssessmentSummary).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.Remark).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.OpportunityNo }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PackageId, x.ActiveMarker }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.NoticeId });
        builder.HasIndex(x => new { x.TenantId, x.Stage, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.NextActionAtUtc });
    }
}

public sealed class OpportunityStageHistoryConfiguration : IEntityTypeConfiguration<OpportunityStageHistory>
{
    public void Configure(EntityTypeBuilder<OpportunityStageHistory> builder)
    {
        builder.ToTable("bidops_opportunity_stage_history");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.FromStage).HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(x => x.ToStage).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.OpportunityId, x.OccurredAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.ToStage, x.OccurredAtUtc });
    }
}

public sealed class OpportunityWatchConfiguration : IEntityTypeConfiguration<OpportunityWatch>
{
    public void Configure(EntityTypeBuilder<OpportunityWatch> builder)
    {
        builder.ToTable("bidops_opportunity_watch");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Remark).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.OpportunityId, x.UserId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UserId, x.Enabled });
    }
}

public sealed class SupplierConfiguration : IEntityTypeConfiguration<Supplier>
{
    public void Configure(EntityTypeBuilder<Supplier> builder)
    {
        builder.ToTable("bidops_supplier");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.SupplierNo).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasColumnType("varchar(300)").HasMaxLength(300).IsRequired();
        builder.Property(x => x.NameNormalized).HasColumnType("varchar(191)").HasMaxLength(191).IsRequired();
        builder.Property(x => x.UnifiedSocialCreditCode).HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(x => x.Region).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.Address).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.ContactName).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.ContactPhone).HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(x => x.ContactEmail).HasColumnType("varchar(256)").HasMaxLength(256);
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.QualityScore).HasPrecision(6, 2);
        builder.Property(x => x.Remark).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.Property(x => x.CreatedFromNoticeTitle).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.CreatedFromSourceUrl).HasColumnType("varchar(1500)").HasMaxLength(1500);
        builder.Property(x => x.LastOutcomeNoticeTitle).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.SupplierNo }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.NameNormalized });
        builder.HasIndex(x => new { x.TenantId, x.UnifiedSocialCreditCode });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.CreatedFromRawNoticeId });
        builder.HasIndex(x => new { x.TenantId, x.LastOutcomeNoticeId });
    }
}

public sealed class BuyerConfiguration : IEntityTypeConfiguration<Buyer>
{
    public void Configure(EntityTypeBuilder<Buyer> builder)
    {
        builder.ToTable("bidops_buyer");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.BuyerNo).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Name).HasColumnType("varchar(300)").HasMaxLength(300).IsRequired();
        builder.Property(x => x.NameNormalized).HasColumnType("varchar(191)").HasMaxLength(191).IsRequired();
        builder.Property(x => x.UnifiedSocialCreditCode).HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(x => x.Region).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.SourceUrl).HasColumnType("varchar(1500)").HasMaxLength(1500);
        builder.Property(x => x.LastProjectCode).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.LastProjectName).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.LastNoticeTitle).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Remark).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.BuyerNo }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.NameNormalized }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.UnifiedSocialCreditCode });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.LastSeenAtUtc });
    }
}

public sealed class SupplierContactConfiguration : IEntityTypeConfiguration<SupplierContact>
{
    public void Configure(EntityTypeBuilder<SupplierContact> builder)
    {
        builder.ToTable("bidops_supplier_contact");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Name).HasColumnType("varchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(x => x.Role).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.Phone).HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(x => x.Email).HasColumnType("varchar(256)").HasMaxLength(256);
        builder.Property(x => x.Remark).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.SupplierId, x.IsPrimary });
        builder.HasIndex(x => new { x.TenantId, x.SupplierId, x.Name });
    }
}

public sealed class BuyerProcurementRecordConfiguration : IEntityTypeConfiguration<BuyerProcurementRecord>
{
    public void Configure(EntityTypeBuilder<BuyerProcurementRecord> builder)
    {
        builder.ToTable("bidops_buyer_procurement_record");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.SourceUrl).HasColumnType("varchar(1500)").HasMaxLength(1500);
        builder.Property(x => x.NoticeTitle).HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
        builder.Property(x => x.NoticeType).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProjectName).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.ProjectCode).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.Region).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.BudgetAmount).HasPrecision(18, 2);
        builder.Property(x => x.SourceHash).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Remark).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.BuyerId, x.PublishTime });
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId });
        builder.HasIndex(x => new { x.TenantId, x.NoticeId });
        builder.HasIndex(x => new { x.TenantId, x.ProjectCode });
        builder.HasIndex(x => new { x.TenantId, x.SourceHash }).IsUnique();
    }
}

public sealed class SupplierCapabilityConfiguration : IEntityTypeConfiguration<SupplierCapability>
{
    public void Configure(EntityTypeBuilder<SupplierCapability> builder)
    {
        builder.ToTable("bidops_supplier_capability");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Category).HasColumnType("varchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(x => x.ProductLine).HasColumnType("varchar(200)").HasMaxLength(200);
        builder.Property(x => x.CapabilityTags).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.Property(x => x.RegionScope).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.QualificationLevel).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.Remark).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.SupplierId, x.Category });
    }
}

public sealed class SupplierEvidenceDocumentConfiguration : IEntityTypeConfiguration<SupplierEvidenceDocument>
{
    public void Configure(EntityTypeBuilder<SupplierEvidenceDocument> builder)
    {
        builder.ToTable("bidops_supplier_evidence_document");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.DocumentName).HasColumnType("varchar(300)").HasMaxLength(300).IsRequired();
        builder.Property(x => x.DocumentType).HasColumnType("varchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(x => x.EvidenceNo).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.IssuedBy).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.FileName).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.FileUrl).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.Property(x => x.StorageProvider).HasColumnType("varchar(64)").HasMaxLength(64);
        builder.Property(x => x.StorageKey).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Remark).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.HasIndex(x => new { x.TenantId, x.SupplierId, x.DocumentType });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.ValidTo });
        builder.HasIndex(x => new { x.TenantId, x.ValidTo });
    }
}

public sealed class OutcomeSupplierRecordConfiguration : IEntityTypeConfiguration<OutcomeSupplierRecord>
{
    public void Configure(EntityTypeBuilder<OutcomeSupplierRecord> builder)
    {
        builder.ToTable("bidops_outcome_supplier_record");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.SourceUrl).HasColumnType("varchar(1500)").HasMaxLength(1500);
        builder.Property(x => x.NoticeTitle).HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
        builder.Property(x => x.NoticeType).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ProjectName).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.ProjectCode).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.BuyerName).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.Region).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.LotNo).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.LotName).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.PackageNo).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.PackageName).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.Category).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.SupplierName).HasColumnType("varchar(300)").HasMaxLength(300).IsRequired();
        builder.Property(x => x.SupplierNameNormalized).HasColumnType("varchar(191)").HasMaxLength(191).IsRequired();
        builder.Property(x => x.OutcomeType).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.AwardAmount).HasPrecision(18, 2);
        builder.Property(x => x.ProcurementAgencyServiceFeeAmount).HasPrecision(18, 2);
        builder.Property(x => x.ExtractionOrder).IsRequired();
        builder.Property(x => x.Currency).HasColumnType("varchar(16)").HasMaxLength(16);
        builder.Property(x => x.EvidenceText).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.ExtractionConfidence).HasPrecision(5, 4);
        builder.Property(x => x.SourceHash).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_CreatedAt");
        builder.HasIndex(x => new { x.TenantId, x.SourceHash })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_SourceHash")
            .IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_RawNotice");
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId, x.ExtractionOrder })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_RawNotice_Order");
        builder.HasIndex(x => new { x.TenantId, x.TenderPackageId })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_Package");
        builder.HasIndex(x => new { x.TenantId, x.BuyerId })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_Buyer");
        builder.HasIndex(x => new { x.TenantId, x.SupplierId })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_Supplier");
        builder.HasIndex(x => new { x.TenantId, x.SupplierNameNormalized })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_SupplierNorm");
        builder.HasIndex(x => new { x.TenantId, x.ProjectCode })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_ProjectCode");
        builder.HasIndex(x => new { x.TenantId, x.PackageNo })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_PackageNo");
        builder.HasIndex(x => new { x.TenantId, x.Category, x.PublishTime })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_Category_Pub");
        builder.HasIndex(x => new { x.TenantId, x.OutcomeType, x.PublishTime })
            .HasDatabaseName("IX_bidops_outcome_record_Tenant_Outcome_Pub");
    }
}

public sealed class AmountCandidateConfiguration : IEntityTypeConfiguration<AmountCandidate>
{
    public void Configure(EntityTypeBuilder<AmountCandidate> builder)
    {
        builder.ToTable("bidops_amount_candidate");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.SourceKind).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceNoticeType).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.SourceTitle).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.SourceFileName).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.SourceLocation).HasColumnType("varchar(256)").HasMaxLength(256);
        builder.Property(x => x.ProjectCode).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.ProjectName).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.LotNo).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.LotName).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.PackageNo).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.PackageName).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.SupplierName).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.AmountType).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.AmountRaw).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.AmountValue).HasColumnType("decimal(18,6)");
        builder.Property(x => x.AmountUnit).HasColumnType("varchar(32)").HasMaxLength(32);
        builder.Property(x => x.Currency).HasColumnType("varchar(16)").HasMaxLength(16);
        builder.Property(x => x.Confidence).HasColumnType("decimal(5,4)");
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.RejectReason).HasColumnType("varchar(500)").HasMaxLength(500);
        builder.Property(x => x.EvidenceText).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.ContextText).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.Property(x => x.ManualRemark).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.Property(x => x.SourceHash).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.SourceHash }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.RawNoticeId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.LifecyclePackageLinkId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.ResultRawNoticeId, x.PackageNo });
        builder.HasIndex(x => new { x.TenantId, x.OutcomeSupplierRecordId });
        builder.HasIndex(x => new { x.TenantId, x.ProcurementDetailStagingId });
        builder.HasIndex(x => new { x.TenantId, x.RawAttachmentId });
    }
}

public sealed class LifecyclePackageLinkConfiguration : IEntityTypeConfiguration<LifecyclePackageLink>
{
    public void Configure(EntityTypeBuilder<LifecyclePackageLink> builder)
    {
        builder.ToTable("bidops_lifecycle_package_link");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.ProjectCode).HasMaxLength(128);
        builder.Property(x => x.ProjectName).HasMaxLength(500);
        builder.Property(x => x.LotNo).HasMaxLength(128);
        builder.Property(x => x.LotName).HasMaxLength(300);
        builder.Property(x => x.PackageNo).HasMaxLength(128);
        builder.Property(x => x.PackageName).HasMaxLength(500);
        builder.Property(x => x.SupplierName).HasMaxLength(300);
        builder.Property(x => x.SupplierNameNormalized).HasMaxLength(300);
        builder.Property(x => x.FinalAwardAmount).HasColumnType("decimal(18,2)");
        builder.Property(x => x.FinalAwardAmountSource).HasMaxLength(128);
        builder.Property(x => x.Currency).HasMaxLength(16).IsRequired();
        builder.Property(x => x.MatchScore).HasColumnType("decimal(5,4)");
        builder.Property(x => x.MatchType).HasMaxLength(64).IsRequired();
        builder.Property(x => x.LinkStatus).HasMaxLength(64).IsRequired();
        builder.Property(x => x.MatchReasonsJson).HasColumnType("longtext");
        builder.Property(x => x.MissingFieldsJson).HasColumnType("longtext");
        builder.Property(x => x.EvidenceJson).HasColumnType("longtext");
        builder.Property(x => x.ManualRemark).HasMaxLength(1000);
        builder.Property(x => x.SourceHash).HasMaxLength(64).IsRequired();
        builder.HasIndex(x => new { x.TenantId, x.SourceHash }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.ProcurementDetailId });
        builder.HasIndex(x => new { x.TenantId, x.ProcurementDetailStagingId });
        builder.HasIndex(x => new { x.TenantId, x.TenderPackageId });
        builder.HasIndex(x => new { x.TenantId, x.CandidateOutcomeRecordId });
        builder.HasIndex(x => new { x.TenantId, x.AwardOutcomeRecordId });
        builder.HasIndex(x => new { x.TenantId, x.LinkStatus, x.RequiresManualReview, x.MatchScore });
        builder.HasIndex(x => new { x.TenantId, x.ProjectCode, x.LotNo, x.PackageNo });
    }
}

public sealed class SupplierMatchRunConfiguration : IEntityTypeConfiguration<SupplierMatchRun>
{
    public void Configure(EntityTypeBuilder<SupplierMatchRun> builder)
    {
        builder.ToTable("bidops_supplier_match_run");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.RunNo).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.RequestedByUserName).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.Property(x => x.CriteriaSummary).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.ErrorMessage).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.RunNo }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PackageId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.BackgroundJobId });
    }
}

public sealed class SupplierMatchResultConfiguration : IEntityTypeConfiguration<SupplierMatchResult>
{
    public void Configure(EntityTypeBuilder<SupplierMatchResult> builder)
    {
        builder.ToTable("bidops_supplier_match_result");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.SupplierNameSnapshot).HasColumnType("varchar(300)").HasMaxLength(300).IsRequired();
        builder.Property(x => x.Score).HasPrecision(6, 2);
        builder.Property(x => x.MatchLevel).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Recommendation).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.RiskFlags).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.Property(x => x.Explanation).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.RunId, x.Rank });
        builder.HasIndex(x => new { x.TenantId, x.PackageId, x.SupplierId });
        builder.HasIndex(x => new { x.TenantId, x.SupplierId, x.CreatedAt });
    }
}

public sealed class MissingEvidenceCheckConfiguration : IEntityTypeConfiguration<MissingEvidenceCheck>
{
    public void Configure(EntityTypeBuilder<MissingEvidenceCheck> builder)
    {
        builder.ToTable("bidops_missing_evidence_check");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.RequiredEvidenceType).HasColumnType("varchar(128)").HasMaxLength(128).IsRequired();
        builder.Property(x => x.RequirementText).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Explanation).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.RunId, x.SupplierId });
        builder.HasIndex(x => new { x.TenantId, x.ResultId });
        builder.HasIndex(x => new { x.TenantId, x.Status, x.CreatedAt });
    }
}

public sealed class GoNoGoDecisionConfiguration : IEntityTypeConfiguration<GoNoGoDecision>
{
    public void Configure(EntityTypeBuilder<GoNoGoDecision> builder)
    {
        builder.ToTable("bidops_go_no_go_decision");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Decision).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Reason).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.RiskSummary).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.DecidedByUserName).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.HasIndex(x => new { x.TenantId, x.PackageId, x.DecidedAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.MatchRunId });
        builder.HasIndex(x => new { x.TenantId, x.SupplierId });
    }
}

public sealed class PursuitConfiguration : IEntityTypeConfiguration<Pursuit>
{
    public void Configure(EntityTypeBuilder<Pursuit> builder)
    {
        builder.ToTable("bidops_pursuit");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.PursuitNo).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Title).HasColumnType("varchar(500)").HasMaxLength(500).IsRequired();
        builder.Property(x => x.SupplierNameSnapshot).HasColumnType("varchar(300)").HasMaxLength(300);
        builder.Property(x => x.Stage).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.ActiveMarker).HasColumnType("varchar(16)").HasMaxLength(16);
        builder.Property(x => x.EstimatedAmount).HasPrecision(18, 2);
        builder.Property(x => x.RiskLevel).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Remark).HasColumnType("varchar(1000)").HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.PursuitNo }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.PackageId, x.ActiveMarker }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.NoticeId });
        builder.HasIndex(x => new { x.TenantId, x.OpportunityId });
        builder.HasIndex(x => new { x.TenantId, x.Stage, x.Status, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.BidDeadlineAtUtc });
    }
}

public sealed class PursuitTaskConfiguration : IEntityTypeConfiguration<PursuitTask>
{
    public void Configure(EntityTypeBuilder<PursuitTask> builder)
    {
        builder.ToTable("bidops_pursuit_task");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.Title).HasColumnType("varchar(300)").HasMaxLength(300).IsRequired();
        builder.Property(x => x.TaskType).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Status).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Description).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.Property(x => x.ResultNote).HasColumnType("varchar(2000)").HasMaxLength(2000);
        builder.HasIndex(x => new { x.TenantId, x.PursuitId, x.Status });
        builder.HasIndex(x => new { x.TenantId, x.OwnerUserId, x.Status, x.DueAtUtc });
        builder.HasIndex(x => new { x.TenantId, x.DueAtUtc });
    }
}

public sealed class PursuitFollowRecordConfiguration : IEntityTypeConfiguration<PursuitFollowRecord>
{
    public void Configure(EntityTypeBuilder<PursuitFollowRecord> builder)
    {
        builder.ToTable("bidops_pursuit_follow_record");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.FollowType).HasColumnType("varchar(64)").HasMaxLength(64).IsRequired();
        builder.Property(x => x.Content).HasColumnType("varchar(2000)").HasMaxLength(2000).IsRequired();
        builder.Property(x => x.CreatedByUserName).HasColumnType("varchar(128)").HasMaxLength(128);
        builder.HasIndex(x => new { x.TenantId, x.PursuitId, x.CreatedAt });
        builder.HasIndex(x => new { x.TenantId, x.NextActionAtUtc });
    }
}
