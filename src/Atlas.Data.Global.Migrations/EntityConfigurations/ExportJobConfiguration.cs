using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Global.Migrations.EntityConfigurations;

public sealed class ExportJobConfiguration : IEntityTypeConfiguration<ExportJob>
{
    public void Configure(EntityTypeBuilder<ExportJob> builder)
    {
        builder.ToTable("ExportJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.BackgroundJobId);

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.UserId)
            .IsRequired();

        builder.Property(x => x.ExportTaskType)
            .IsRequired()
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.ResourceCode)
            .IsRequired()
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.PermissionCode)
            .IsRequired()
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.Format)
            .IsRequired()
            .HasColumnType("varchar(50)")
            .HasMaxLength(50);

        builder.Property(x => x.QueryJson)
            .IsRequired()
            .HasColumnType("longtext");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExportJobStatus.Pending);

        builder.Property(x => x.Progress)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.ProcessedRows)
            .IsRequired()
            .HasDefaultValue(0L);

        builder.Property(x => x.FileName)
            .HasColumnType("varchar(300)")
            .HasMaxLength(300);

        builder.Property(x => x.ContentType)
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.StorageProvider)
            .HasColumnType("varchar(100)")
            .HasMaxLength(100);

        builder.Property(x => x.StorageKey)
            .HasColumnType("varchar(500)")
            .HasMaxLength(500);

        builder.Property(x => x.Sha256)
            .HasColumnType("varchar(64)")
            .HasMaxLength(64);

        builder.Property(x => x.QueryHash)
            .IsRequired()
            .HasColumnType("varchar(64)")
            .HasMaxLength(64);

        builder.Property(x => x.RequestedAtUtc)
            .IsRequired();

        builder.Property(x => x.ExpiresAtUtc)
            .IsRequired();

        builder.Property(x => x.LastError)
            .HasColumnType("text");

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false);

        builder.HasIndex(x => x.BackgroundJobId)
            .IsUnique()
            .HasDatabaseName("UX_ExportJobs_BackgroundJobId");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.RequestedAtUtc })
            .HasDatabaseName("IX_ExportJobs_TenantId_UserId_RequestedAtUtc");

        builder.HasIndex(x => new { x.TenantId, x.Status })
            .HasDatabaseName("IX_ExportJobs_TenantId_Status");

        builder.HasIndex(x => x.ExpiresAtUtc)
            .HasDatabaseName("IX_ExportJobs_ExpiresAtUtc");

        builder.HasIndex(x => x.ExportTaskType)
            .HasDatabaseName("IX_ExportJobs_ExportTaskType");

        builder.HasIndex(x => x.ResourceCode)
            .HasDatabaseName("IX_ExportJobs_ResourceCode");
    }
}
