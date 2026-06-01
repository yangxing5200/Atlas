using Atlas.Core.Entities.Global;
using Atlas.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Global.Migrations.EntityConfigurations;

public sealed class TenantSchemaMigrationStateConfiguration : IEntityTypeConfiguration<TenantSchemaMigrationState>
{
    public void Configure(EntityTypeBuilder<TenantSchemaMigrationState> builder)
    {
        builder.ToTable("TenantSchemaMigrationStates");

        builder.HasKey(x => x.Id);
        builder.Property(x => x.Id).ValueGeneratedNever();

        builder.Property(x => x.CurrentVersion)
            .HasColumnType("varchar(150)")
            .HasMaxLength(150);

        builder.Property(x => x.TargetVersion)
            .HasColumnType("varchar(150)")
            .HasMaxLength(150);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(TenantSchemaMigrationStatus.Pending);

        builder.Property(x => x.LastError)
            .HasColumnType("text");

        builder.Property(x => x.RetryCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.UpdatedAtUtc)
            .IsRequired();

        builder.Property(x => x.CreatedAt)
            .IsRequired();

        builder.Property(x => x.UpdatedAt)
            .IsRequired(false);

        builder.HasIndex(x => x.TenantId)
            .IsUnique()
            .HasDatabaseName("UX_TenantSchemaMigrationStates_TenantId");

        builder.HasIndex(x => new { x.Status, x.UpdatedAtUtc })
            .HasDatabaseName("IX_TenantSchemaMigrationStates_Status_UpdatedAtUtc");
    }
}
