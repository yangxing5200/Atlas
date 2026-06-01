using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Tenant.Migrations.EntityConfigurations;

public sealed class RoleConfiguration : BaseEntityConfiguration<Role>
{
    public override void Configure(EntityTypeBuilder<Role> builder)
    {
        base.Configure(builder);

        builder.ToTable("Roles");

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Scope).IsRequired().HasConversion<int>().HasDefaultValue(PermissionScope.Tenant);
        builder.Property(x => x.StoreId).IsRequired(false);
        builder.Property(x => x.IsSystem).IsRequired().HasDefaultValue(false);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("UX_Roles_TenantId_Code");

        builder.HasIndex(x => new { x.TenantId, x.Scope, x.StoreId })
            .HasDatabaseName("IX_Roles_Tenant_Scope_Store");
    }
}

public sealed class PermissionConfiguration : BaseEntityConfiguration<Permission>
{
    public override void Configure(EntityTypeBuilder<Permission> builder)
    {
        base.Configure(builder);

        builder.ToTable("Permissions");

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.Code).IsRequired().HasMaxLength(150);
        builder.Property(x => x.Name).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(500);
        builder.Property(x => x.Module).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Scope).IsRequired().HasConversion<int>().HasDefaultValue(PermissionScope.Tenant);
        builder.Property(x => x.IsBuiltIn).IsRequired().HasDefaultValue(true);
        builder.Property(x => x.IsEnabled).IsRequired().HasDefaultValue(true);

        builder.HasIndex(x => new { x.TenantId, x.Code })
            .IsUnique()
            .HasDatabaseName("UX_Permissions_TenantId_Code");

        builder.HasIndex(x => new { x.TenantId, x.Module, x.Scope })
            .HasDatabaseName("IX_Permissions_Tenant_Module_Scope");
    }
}

public sealed class RolePermissionConfiguration : BaseEntityConfiguration<RolePermission>
{
    public override void Configure(EntityTypeBuilder<RolePermission> builder)
    {
        base.Configure(builder);

        builder.ToTable("RolePermissions");

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.RoleId).IsRequired();
        builder.Property(x => x.PermissionId).IsRequired();
        builder.Property(x => x.GrantedAt).IsRequired();
        builder.Property(x => x.GrantedBy).IsRequired(false);

        builder.HasIndex(x => new { x.TenantId, x.RoleId, x.PermissionId })
            .IsUnique()
            .HasDatabaseName("UX_RolePermissions_Tenant_Role_Permission");

        builder.HasIndex(x => new { x.TenantId, x.PermissionId })
            .HasDatabaseName("IX_RolePermissions_Tenant_Permission");

        builder.HasOne(x => x.Role)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.RoleId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(x => x.Permission)
            .WithMany(x => x.RolePermissions)
            .HasForeignKey(x => x.PermissionId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class RefreshTokenConfiguration : BaseEntityConfiguration<RefreshToken>
{
    public override void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        base.Configure(builder);

        builder.ToTable("RefreshTokens");

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserId).IsRequired();
        builder.Property(x => x.StoreId).IsRequired(false);
        builder.Property(x => x.SessionId).IsRequired().HasMaxLength(64);
        builder.Property(x => x.TokenHash).IsRequired().HasMaxLength(128);
        builder.Property(x => x.ExpiresAtUtc).IsRequired();
        builder.Property(x => x.RevokedAtUtc).IsRequired(false);
        builder.Property(x => x.RevokedReason).HasMaxLength(100);
        builder.Property(x => x.CreatedByIp).HasMaxLength(50);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.ReplacedByTokenId).IsRequired(false);

        builder.HasIndex(x => new { x.TenantId, x.TokenHash })
            .IsUnique()
            .HasDatabaseName("UX_RefreshTokens_Tenant_TokenHash");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.SessionId })
            .HasDatabaseName("IX_RefreshTokens_Tenant_User_Session");

        builder.HasIndex(x => new { x.TenantId, x.ExpiresAtUtc, x.RevokedAtUtc })
            .HasDatabaseName("IX_RefreshTokens_Tenant_Expiry_Status");
    }
}

public sealed class AuditEventConfiguration : BaseEntityConfiguration<AuditEvent>
{
    public override void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        base.Configure(builder);

        builder.ToTable("AuditEvents");

        builder.Property(x => x.TenantId).IsRequired();
        builder.Property(x => x.UserId).IsRequired(false);
        builder.Property(x => x.StoreId).IsRequired(false);
        builder.Property(x => x.SessionId).HasMaxLength(64);
        builder.Property(x => x.TraceId).HasMaxLength(100);
        builder.Property(x => x.Category).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Action).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Outcome).IsRequired().HasConversion<int>().HasDefaultValue(AuditEventOutcome.Succeeded);
        builder.Property(x => x.EntityType).HasMaxLength(100);
        builder.Property(x => x.EntityId).IsRequired(false);
        builder.Property(x => x.IpAddress).HasMaxLength(50);
        builder.Property(x => x.UserAgent).HasMaxLength(500);
        builder.Property(x => x.Metadata).HasMaxLength(4000);
        builder.Property(x => x.ErrorMessage).HasMaxLength(1000);

        builder.HasIndex(x => new { x.TenantId, x.CreatedAt })
            .HasDatabaseName("IX_AuditEvents_Tenant_CreatedAt");

        builder.HasIndex(x => new { x.TenantId, x.UserId, x.CreatedAt })
            .HasDatabaseName("IX_AuditEvents_Tenant_User_CreatedAt");

        builder.HasIndex(x => new { x.TenantId, x.Category, x.Action, x.CreatedAt })
            .HasDatabaseName("IX_AuditEvents_Tenant_Category_Action");

        builder.HasIndex(x => x.TraceId)
            .HasDatabaseName("IX_AuditEvents_TraceId");
    }
}
