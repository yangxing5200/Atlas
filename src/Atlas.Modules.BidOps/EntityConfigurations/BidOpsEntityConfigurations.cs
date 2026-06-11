using Atlas.Modules.BidOps.Entities;
using Atlas.Modules.BidOps.Entities.Crawling;
using Atlas.Modules.BidOps.Entities.Staging;
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

public sealed class ReviewTaskConfiguration : IEntityTypeConfiguration<ReviewTask>
{
    public void Configure(EntityTypeBuilder<ReviewTask> builder)
    {
        builder.ToTable("bidops_review_task");
        builder.ConfigureTenantEntity();
        builder.Property(x => x.BizType).HasMaxLength(128).IsRequired();
        builder.Property(x => x.TaskTitle).HasMaxLength(500).IsRequired();
        builder.Property(x => x.Status).HasConversion<int>().IsRequired();
        builder.Property(x => x.Decision).HasMaxLength(128);
        builder.Property(x => x.Remark).HasMaxLength(1000);
        builder.HasIndex(x => new { x.TenantId, x.BizType, x.BizId }).IsUnique();
        builder.HasIndex(x => new { x.TenantId, x.Status, x.Priority, x.CreatedAt });
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
