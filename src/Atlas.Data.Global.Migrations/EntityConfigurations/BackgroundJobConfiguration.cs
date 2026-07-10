using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Global.Migrations.EntityConfigurations;

public sealed class BackgroundJobConfiguration : IEntityTypeConfiguration<BackgroundJob>
{
    public void Configure(EntityTypeBuilder<BackgroundJob> builder)
    {
        builder.ToTable("BackgroundJobs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id)
            .ValueGeneratedNever();

        builder.Property(x => x.JobType)
            .IsRequired()
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.JobName)
            .IsRequired()
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.Queue)
            .IsRequired()
            .HasColumnType("varchar(100)")
            .HasMaxLength(100)
            .HasDefaultValue("default");

        builder.Property(x => x.DeduplicationKey)
            .HasColumnType("varchar(300)")
            .HasMaxLength(300);

        builder.Property(x => x.SourceModule)
            .HasColumnType($"varchar({BackgroundJobBusinessConstants.SourceModuleMaxLength})")
            .HasMaxLength(BackgroundJobBusinessConstants.SourceModuleMaxLength);

        builder.Property(x => x.BusinessType)
            .HasColumnType($"varchar({BackgroundJobBusinessConstants.BusinessTypeMaxLength})")
            .HasMaxLength(BackgroundJobBusinessConstants.BusinessTypeMaxLength);

        builder.Property(x => x.CorrelationId)
            .HasColumnType($"varchar({BackgroundJobBusinessConstants.CorrelationIdMaxLength})")
            .HasMaxLength(BackgroundJobBusinessConstants.CorrelationIdMaxLength);

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("longtext");

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(BackgroundJobStatus.Pending);

        builder.Property(x => x.Priority)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.AvailableAtUtc)
            .IsRequired();

        builder.Property(x => x.LockedBy)
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.MaxAttempts)
            .IsRequired()
            .HasDefaultValue(5);

        builder.Property(x => x.CancellationRequestedBy)
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.CancellationReason)
            .HasColumnType("text");

        builder.Property(x => x.LastError)
            .HasColumnType("text");

        builder.Property(x => x.Result)
            .HasColumnType("mediumtext")
            .HasMaxLength(1_000_000);

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false);

        builder.HasIndex(x => x.JobType)
            .HasDatabaseName("IX_BackgroundJobs_JobType");

        builder.HasIndex(x => x.Queue)
            .HasDatabaseName("IX_BackgroundJobs_Queue");

        builder.HasIndex(x => new { x.TenantId, x.DeduplicationKey })
            .IsUnique()
            .HasDatabaseName("UX_BackgroundJobs_Tenant_DeduplicationKey");

        builder.HasIndex(x => new { x.Queue, x.Status, x.AvailableAtUtc, x.NextAttemptAtUtc, x.Priority })
            .HasDatabaseName("IX_BackgroundJobs_DispatchDue");

        builder.HasIndex(x => new { x.Queue, x.Status, x.LockedAtUtc })
            .HasDatabaseName("IX_BackgroundJobs_RunningLocks");

        builder.HasIndex(x => new { x.Status, x.CancellationRequestedAt })
            .HasDatabaseName("IX_BackgroundJobs_CancellationRequested");

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
            .HasDatabaseName("IX_BackgroundJobs_Tenant_CreatedAt");

        builder.HasIndex(x => new { x.TenantId, x.SourceModule, x.BusinessType, x.BusinessId, x.CreatedAt })
            .HasDatabaseName("IX_BackgroundJobs_Tenant_BusinessLink");

        builder.HasIndex(x => new { x.TenantId, x.CorrelationId, x.CreatedAt })
            .HasDatabaseName("IX_BackgroundJobs_Tenant_Correlation");
    }
}
