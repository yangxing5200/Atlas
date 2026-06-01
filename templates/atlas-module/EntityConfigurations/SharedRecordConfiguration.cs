using Atlas.ModuleTemplate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.ModuleTemplate.EntityConfigurations;

public sealed class SharedRecordConfiguration : IEntityTypeConfiguration<SharedRecord>
{
    public void Configure(EntityTypeBuilder<SharedRecord> builder)
    {
        builder.ToTable("SharedRecords");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(record => new { record.TenantId, record.StoreId, record.Name });
    }
}
