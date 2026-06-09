using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;
using Atlas.Core.Entities.Tenant;

namespace Atlas.Data.Tenant.EntityConfigurations
{
    public class ProductConfiguration : SharedEntityConfiguration<Product>
    {
        public override void Configure(EntityTypeBuilder<Product> builder)
        {
            base.Configure(builder);

            builder.ToTable("Products");

            builder.Property(x => x.Name)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.Price)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(x => x.Description)
                .HasMaxLength(2000);

            builder.Property(x => x.SourceStoreId)
                .IsRequired(false);

            builder.Property(x => x.IsCustomized)
                .IsRequired()
                .HasDefaultValue(false);

            builder.HasIndex(x => x.SourceStoreId)
                .HasDatabaseName("IX_Products_SourceStoreId");

            builder.HasIndex(x => new { x.TenantId, x.StoreId, x.IsCustomized })
                .HasDatabaseName("IX_Products_TenantId_StoreId_IsCustomized");
        }
    }
    // ============================================
    // Member Configuration
    // ============================================
    public class MemberConfiguration : SharedEntityConfiguration<Member>
    {
        public override void Configure(EntityTypeBuilder<Member> builder)
        {
            base.Configure(builder);

            builder.ToTable("Members");

            builder.Property(x => x.MemberName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.Phone)
                .IsRequired()
                .HasMaxLength(20);

            builder.Property(x => x.Email)
                .HasMaxLength(100);

            builder.Property(x => x.Points)
                .IsRequired()
                .HasDefaultValue(0);

            builder.HasIndex(x => new { x.TenantId, x.Phone })
                .HasDatabaseName("IX_Members_TenantId_Phone");
        }
    }

    // ============================================
    // Promotion Configuration
    // ============================================
    public class PromotionConfiguration : SharedEntityConfiguration<Promotion>
    {
        public override void Configure(EntityTypeBuilder<Promotion> builder)
        {
            base.Configure(builder);

            builder.ToTable("Promotions");

            builder.Property(x => x.Title)
                .IsRequired()
                .HasMaxLength(200);

            builder.Property(x => x.StartTime)
                .IsRequired();

            builder.Property(x => x.EndTime)
                .IsRequired();

            builder.Property(x => x.DiscountRate)
                .IsRequired()
                .HasColumnType("decimal(5,2)");

            builder.Property(x => x.SourceStoreId)
                .IsRequired(false);

            builder.HasIndex(x => new { x.TenantId, x.StartTime, x.EndTime })
                .HasDatabaseName("IX_Promotions_TenantId_StartTime_EndTime");

            builder.HasIndex(x => x.SourceStoreId)
                .HasDatabaseName("IX_Promotions_SourceStoreId");
        }
    }

    // ============================================
    // Order Configuration
    // ============================================
    public class OrderConfiguration : StoreOnlyVersionedEntityConfiguration<Order>
    {
        public override void Configure(EntityTypeBuilder<Order> builder)
        {
            base.Configure(builder);

            builder.ToTable("Orders");

            builder.Property(x => x.OrderNo)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.MemberId)
                .IsRequired();

            builder.Property(x => x.TotalAmount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(x => x.Status)
                .IsRequired()
                .HasConversion<int>();

            builder.HasIndex(x => new { x.TenantId, x.OrderNo })
                .IsUnique()
                .HasDatabaseName("IX_Orders_TenantId_OrderNo");

            builder.HasIndex(x => new { x.TenantId, x.MemberId })
                .HasDatabaseName("IX_Orders_TenantId_MemberId");

            builder.HasIndex(x => new { x.TenantId, x.StoreId, x.Status })
                .HasDatabaseName("IX_Orders_TenantId_StoreId_Status");
        }
    }

    // ============================================
    // Inventory Configuration
    // ============================================
    public class InventoryConfiguration : StoreOnlyEntityConfiguration<Inventory>
    {
        public override void Configure(EntityTypeBuilder<Inventory> builder)
        {
            base.Configure(builder);

            builder.ToTable("Inventories");

            builder.Property(x => x.ProductId)
                .IsRequired();

            builder.Property(x => x.Quantity)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.SafetyStock)
                .IsRequired()
                .HasDefaultValue(0);

            builder.HasIndex(x => new { x.TenantId, x.StoreId, x.ProductId })
                .IsUnique()
                .HasDatabaseName("IX_Inventories_TenantId_StoreId_ProductId");
        }
    }

    // ============================================
    // CashierRecord Configuration
    // ============================================
    public class CashierRecordConfiguration : StoreOnlyEntityConfiguration<CashierRecord>
    {
        public override void Configure(EntityTypeBuilder<CashierRecord> builder)
        {
            base.Configure(builder);

            builder.ToTable("CashierRecords");

            builder.Property(x => x.OrderId)
                .IsRequired();

            builder.Property(x => x.Amount)
                .IsRequired()
                .HasColumnType("decimal(18,2)");

            builder.Property(x => x.PaymentMethod)
                .IsRequired()
                .HasConversion<int>();

            builder.Property(x => x.PaidAt)
                .IsRequired();

            builder.HasIndex(x => new { x.TenantId, x.OrderId })
                .HasDatabaseName("IX_CashierRecords_TenantId_OrderId");

            builder.HasIndex(x => new { x.TenantId, x.StoreId, x.PaidAt })
                .HasDatabaseName("IX_CashierRecords_TenantId_StoreId_PaidAt");
        }
    }
}
