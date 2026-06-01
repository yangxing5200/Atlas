using Atlas.Core.Authorization;
using Atlas.Core.Entities.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Global.Migrations.EntityConfigurations;

public sealed class CapabilityConfiguration : IEntityTypeConfiguration<Capability>
{
    public void Configure(EntityTypeBuilder<Capability> builder)
    {
        builder.ToTable("Capabilities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(100).HasDefaultValue(string.Empty);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.SourceModule).IsRequired().HasMaxLength(150).HasDefaultValue(string.Empty);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_Capabilities_Code");
        builder.HasIndex(x => new { x.Category, x.IsEnabled }).HasDatabaseName("IX_Capabilities_Category_Enabled");
    }
}

public sealed class FeaturePackageConfiguration : IEntityTypeConfiguration<FeaturePackage>
{
    public void Configure(EntityTypeBuilder<FeaturePackage> builder)
    {
        builder.ToTable("FeaturePackages");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Type).IsRequired().HasConversion<int>().HasDefaultValue(AtlasPackageType.Edition);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.SourceModule).IsRequired().HasMaxLength(150).HasDefaultValue(string.Empty);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_FeaturePackages_Code");
        builder.HasIndex(x => new { x.Type, x.IsEnabled }).HasDatabaseName("IX_FeaturePackages_Type_Enabled");
    }
}

public sealed class PackageCapabilityConfiguration : IEntityTypeConfiguration<PackageCapability>
{
    public void Configure(EntityTypeBuilder<PackageCapability> builder)
    {
        builder.ToTable("PackageCapabilities");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.PackageCode).IsRequired().HasMaxLength(150);
        builder.Property(x => x.CapabilityCode).IsRequired().HasMaxLength(150);
        builder.Property(x => x.LimitJson).HasColumnType("longtext");
        builder.Property(x => x.OptionJson).HasColumnType("longtext");
        builder.Property(x => x.SourceModule).IsRequired().HasMaxLength(150).HasDefaultValue(string.Empty);
        builder.HasIndex(x => new { x.PackageCode, x.CapabilityCode })
            .IsUnique()
            .HasDatabaseName("UX_PackageCapabilities_Package_Capability");
        builder.HasIndex(x => x.CapabilityCode).HasDatabaseName("IX_PackageCapabilities_Capability");
    }
}

public sealed class TenantEntitlementConfiguration : IEntityTypeConfiguration<TenantEntitlement>
{
    public void Configure(EntityTypeBuilder<TenantEntitlement> builder)
    {
        builder.ToTable("TenantEntitlements");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.SubjectType).IsRequired().HasConversion<int>().HasDefaultValue(AtlasEntitlementSubjectType.Tenant);
        builder.Property(x => x.SubjectId).IsRequired();
        builder.Property(x => x.PackageCode).HasMaxLength(150);
        builder.Property(x => x.CapabilityCode).HasMaxLength(150);
        builder.Property(x => x.Source).IsRequired().HasConversion<int>();
        builder.Property(x => x.StartAtUtc).IsRequired();
        builder.Property(x => x.EndAtUtc).IsRequired(false);
        builder.Property(x => x.Status).IsRequired().HasConversion<int>().HasDefaultValue(AtlasEntitlementStatus.Active);
        builder.Property(x => x.OptionOverrideJson).HasColumnType("longtext");
        builder.HasIndex(x => new { x.TenantId, x.SubjectType, x.SubjectId, x.Status })
            .HasDatabaseName("IX_TenantEntitlements_Subject_Status");
        builder.HasIndex(x => new { x.TenantId, x.PackageCode })
            .HasDatabaseName("IX_TenantEntitlements_Package");
        builder.HasIndex(x => new { x.TenantId, x.CapabilityCode })
            .HasDatabaseName("IX_TenantEntitlements_Capability");
    }
}

public sealed class MenuItemConfiguration : IEntityTypeConfiguration<MenuItem>
{
    public void Configure(EntityTypeBuilder<MenuItem> builder)
    {
        builder.ToTable("MenuItems");
        builder.HasKey(x => x.Id);
        builder.Property(x => x.Code).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Route).IsRequired().HasMaxLength(300);
        builder.Property(x => x.ParentCode).HasMaxLength(150);
        builder.Property(x => x.Icon).HasMaxLength(100);
        builder.Property(x => x.SortOrder).IsRequired().HasDefaultValue(0);
        builder.Property(x => x.VisibleWhenJson).HasColumnType("longtext");
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.SourceModule).IsRequired().HasMaxLength(150).HasDefaultValue(string.Empty);
        builder.HasIndex(x => x.Code).IsUnique().HasDatabaseName("UX_MenuItems_Code");
        builder.HasIndex(x => new { x.ParentCode, x.SortOrder }).HasDatabaseName("IX_MenuItems_Parent_Sort");
    }
}
