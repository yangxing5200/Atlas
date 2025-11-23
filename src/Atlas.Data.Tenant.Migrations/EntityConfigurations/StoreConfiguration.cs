using Atlas.Core.Entities.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Tenant.Migrations.EntityConfigurations
{
    public class StoreConfiguration : VersionedEntityConfiguration<Store>
    {
        public override void Configure(EntityTypeBuilder<Store> builder)
        {
            base.Configure(builder);

            builder.ToTable("Stores");

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Type)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(x => x.ParentStoreId)
                .IsRequired(false);

            builder.Property(x => x.IsActive)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.Address)
                .HasMaxLength(500);
            builder.Property(x => x.Province)
                .HasMaxLength(50);
            builder.Property(x => x.City)
                .HasMaxLength(50);
            builder.Property(x => x.District)
                .HasMaxLength(100);

            builder.Property(x => x.ContactPhone)
                .HasMaxLength(50);

            builder.Property(x => x.ContactPerson)
                .HasMaxLength(100);

            builder.HasIndex(x => x.TenantId)
                .HasDatabaseName("IX_Stores_TenantId");

            builder.HasIndex(x => x.ParentStoreId)
                .HasDatabaseName("IX_Stores_ParentStoreId");

            builder.HasIndex(x => new { x.TenantId, x.Type })
                .HasDatabaseName("IX_Stores_TenantId_Type");

            builder.HasIndex(x => new { x.TenantId, x.IsActive })
                .HasDatabaseName("IX_Stores_TenantId_IsActive");

            builder.HasOne(x => x.ParentStore)
                .WithMany(x => x.ChildStores)
                .HasForeignKey(x => x.ParentStoreId)
                .OnDelete(DeleteBehavior.Restrict);
        }
    }
}