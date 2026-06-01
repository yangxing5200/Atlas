using Atlas.ModuleTemplate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.ModuleTemplate.EntityConfigurations;

public sealed class TenantRecordConfiguration : IEntityTypeConfiguration<TenantRecord>
{
    public void Configure(EntityTypeBuilder<TenantRecord> builder)
    {
        builder.ToTable("TenantRecords");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(record => new { record.TenantId, record.Name });
    }
}
