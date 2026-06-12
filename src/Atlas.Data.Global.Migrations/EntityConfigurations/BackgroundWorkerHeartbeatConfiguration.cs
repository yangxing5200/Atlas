using Atlas.Core.Entities.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Global.Migrations.EntityConfigurations;

public sealed class BackgroundWorkerHeartbeatConfiguration : IEntityTypeConfiguration<BackgroundWorkerHeartbeat>
{
    public void Configure(EntityTypeBuilder<BackgroundWorkerHeartbeat> builder)
    {
        builder.ToTable("BackgroundWorkerHeartbeats");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.WorkerId)
            .IsRequired()
            .HasColumnType("varchar(191)")
            .HasMaxLength(191);

        builder.Property(x => x.HostName)
            .IsRequired()
            .HasColumnType("varchar(256)")
            .HasMaxLength(256);

        builder.Property(x => x.RuntimeMode)
            .IsRequired()
            .HasColumnType("varchar(64)")
            .HasMaxLength(64);

        builder.Property(x => x.QueuesJson)
            .IsRequired()
            .HasColumnType("longtext");

        builder.Property(x => x.CurrentJobType)
            .HasColumnType("varchar(200)")
            .HasMaxLength(200);

        builder.Property(x => x.CurrentQueue)
            .HasColumnType("varchar(100)")
            .HasMaxLength(100);

        builder.Property(x => x.StartedAtUtc).IsRequired();
        builder.Property(x => x.LastSeenAtUtc).IsRequired();
        builder.Property(x => x.CreatedAt).IsRequired();
        builder.Property(x => x.UpdatedAt).IsRequired(false);

        builder.HasIndex(x => x.WorkerId)
            .IsUnique()
            .HasDatabaseName("UX_BackgroundWorkerHeartbeats_WorkerId");

        builder.HasIndex(x => x.LastSeenAtUtc)
            .HasDatabaseName("IX_BackgroundWorkerHeartbeats_LastSeenAtUtc");

        builder.HasIndex(x => new { x.RuntimeMode, x.LastSeenAtUtc })
            .HasDatabaseName("IX_BackgroundWorkerHeartbeats_Runtime_LastSeen");

        builder.HasIndex(x => x.CurrentJobId)
            .HasDatabaseName("IX_BackgroundWorkerHeartbeats_CurrentJobId");
    }
}
