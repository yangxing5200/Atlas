using Atlas.ModuleTemplate.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.ModuleTemplate.EntityConfigurations;

public sealed class StoreOnlyRecordConfiguration : IEntityTypeConfiguration<StoreOnlyRecord>
{
    public void Configure(EntityTypeBuilder<StoreOnlyRecord> builder)
    {
        builder.ToTable("StoreOnlyRecords");
        builder.HasKey(record => record.Id);

        builder.Property(record => record.Name)
            .HasMaxLength(128)
            .IsRequired();

        builder.HasIndex(record => new { record.TenantId, record.StoreId, record.Name });
    }
}
