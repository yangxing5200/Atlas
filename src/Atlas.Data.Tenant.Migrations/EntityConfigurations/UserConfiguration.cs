using Atlas.Core.Entities.Tenant;
using Atlas.Core.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Migrations.EntityConfigurations
{
    // ========================================
    // User 实体配置
    // ========================================

    /// <summary>
    /// 用户表配置
    /// </summary>
    public class UserConfiguration : VersionedEntityConfiguration<User>
    {
        public override void Configure(EntityTypeBuilder<User> builder)
        {
            base.Configure(builder);

            builder.ToTable("Users");

            #region 基础字段

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.UserName)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.PasswordHash)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.RealName)
                .IsRequired()
                .HasMaxLength(100);

            builder.Property(x => x.NickName)
                .HasMaxLength(100);

            builder.Property(x => x.Phone)
                .HasMaxLength(20);

            builder.Property(x => x.Email)
                .HasMaxLength(100);

            builder.Property(x => x.Avatar)
                .HasMaxLength(500);

            builder.Property(x => x.Gender)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(Gender.Unknown);

            #endregion

            #region 安全相关

            builder.Property(x => x.TokenVersion)
                .IsRequired()
                .HasDefaultValue(1);

            builder.Property(x => x.LastLoginAt)
                .IsRequired(false);

            builder.Property(x => x.LastLoginIp)
                .HasMaxLength(50);

            builder.Property(x => x.LoginFailedCount)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.LockoutEndAt)
                .IsRequired(false);

            builder.Property(x => x.PasswordExpiresAt)
                .IsRequired(false);

            builder.Property(x => x.MustChangePassword)
                .IsRequired()
                .HasDefaultValue(false);

            #endregion

            #region 状态与权限

            builder.Property(x => x.Type)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(UserType.Employee);

            builder.Property(x => x.Status)
                .IsRequired()
                .HasConversion<int>()
                .HasDefaultValue(UserStatus.Active);

            builder.Property(x => x.IsActivated)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.RoleIds)
                .HasMaxLength(500);

            builder.Property(x => x.DefaultStoreId)
                .IsRequired(false);

            #endregion

            #region 业务信息

            builder.Property(x => x.EmployeeNo)
                .HasMaxLength(50);

            builder.Property(x => x.DepartmentId)
                .IsRequired(false);

            builder.Property(x => x.Position)
                .HasMaxLength(100);

            builder.Property(x => x.HireDate)
                .IsRequired(false);

            builder.Property(x => x.Remark)
                .HasMaxLength(1000);

            builder.Property(x => x.ExtendedData)
                .HasMaxLength(4000);

            #endregion

            #region 索引

            // 用户名索引；唯一性由服务层按活跃用户校验，避免 MySQL 无过滤索引时软删除后无法复用用户名
            builder.HasIndex(x => new { x.TenantId, x.UserName })
                .HasDatabaseName("IX_Users_TenantId_UserName");

            // 手机号索引（用于手机号登录）
            builder.HasIndex(x => new { x.TenantId, x.Phone })
                .HasDatabaseName("IX_Users_TenantId_Phone");

            // 邮箱索引（用于邮箱登录）
            builder.HasIndex(x => new { x.TenantId, x.Email })
                .HasDatabaseName("IX_Users_TenantId_Email");

            // 员工编号索引
            builder.HasIndex(x => new { x.TenantId, x.EmployeeNo })
                .HasDatabaseName("IX_Users_TenantId_EmployeeNo");

            // 状态索引（查询活跃用户）
            builder.HasIndex(x => new { x.TenantId, x.Status, x.IsDeleted })
                .HasDatabaseName("IX_Users_TenantId_Status_IsDeleted");

            // 用户类型索引
            builder.HasIndex(x => new { x.TenantId, x.Type })
                .HasDatabaseName("IX_Users_TenantId_Type");

            // TokenVersion索引（用于快速查询版本号）
            builder.HasIndex(x => new { x.Id, x.TokenVersion })
                .HasDatabaseName("IX_Users_Id_TokenVersion");

            // 默认门店索引
            builder.HasIndex(x => x.DefaultStoreId)
                .HasDatabaseName("IX_Users_DefaultStoreId");

            #endregion

            #region 外键关系

            // 默认门店外键
            builder.HasOne(x => x.DefaultStore)
                .WithMany()
                .HasForeignKey(x => x.DefaultStoreId)
                .OnDelete(DeleteBehavior.Restrict);

            // 用户-门店关联（一对多）
            builder.HasMany(x => x.UserStores)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 用户-角色关联（一对多）
            builder.HasMany(x => x.UserRoles)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            // 登录日志（一对多）
            builder.HasMany(x => x.LoginLogs)
                .WithOne(x => x.User)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            #endregion
        }
    }

    // ========================================
    // UserStore 实体配置（用户-门店关联表）
    // ========================================

    /// <summary>
    /// 用户-门店关联表配置
    /// </summary>
    public class UserStoreConfiguration : BaseEntityConfiguration<UserStore>
    {
        public override void Configure(EntityTypeBuilder<UserStore> builder)
        {
            base.Configure(builder);

            builder.ToTable("UserStores");

            #region 字段配置

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.StoreId)
                .IsRequired();

            builder.Property(x => x.IsPrimary)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(x => x.Permission)
                .HasMaxLength(50);

            builder.Property(x => x.EffectiveFrom)
                .IsRequired(false);

            builder.Property(x => x.EffectiveTo)
                .IsRequired(false);

            #endregion

            #region 索引

            // 唯一索引：同一用户在同一门店只能有一条记录
            builder.HasIndex(x => new { x.UserId, x.StoreId })
                .IsUnique()
                .HasDatabaseName("IX_UserStores_UserId_StoreId");

            // 用户ID索引（查询用户的所有门店）
            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_UserStores_UserId");

            // 门店ID索引（查询门店的所有用户）
            builder.HasIndex(x => x.StoreId)
                .HasDatabaseName("IX_UserStores_StoreId");

            // 租户+用户索引
            builder.HasIndex(x => new { x.TenantId, x.UserId })
                .HasDatabaseName("IX_UserStores_TenantId_UserId");

            // 主门店索引（快速查找用户的主门店）
            builder.HasIndex(x => new { x.UserId, x.IsPrimary })
                .HasDatabaseName("IX_UserStores_UserId_IsPrimary");

            #endregion

            #region 外键关系

            builder.HasOne(x => x.User)
                .WithMany(x => x.UserStores)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Store)
                .WithMany()
                .HasForeignKey(x => x.StoreId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion
        }
    }

    // ========================================
    // UserRole 实体配置（用户-角色关联表）
    // ========================================

    /// <summary>
    /// 用户-角色关联表配置
    /// </summary>
    public class UserRoleConfiguration : BaseEntityConfiguration<UserRole>
    {
        public override void Configure(EntityTypeBuilder<UserRole> builder)
        {
            base.Configure(builder);

            builder.ToTable("UserRoles");

            #region 字段配置

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.UserId)
                .IsRequired();

            builder.Property(x => x.RoleId)
                .IsRequired();

            builder.Property(x => x.StoreId)
                .IsRequired()
                .HasDefaultValue(0);

            builder.Property(x => x.GrantedAt)
                .IsRequired();

            builder.Property(x => x.GrantedBy)
                .IsRequired();

            #endregion

            #region 索引

            // 唯一索引：同一租户内，同一用户在同一门店范围不能重复授予同一角色。
            builder.HasIndex(x => new { x.TenantId, x.UserId, x.RoleId, x.StoreId })
                .IsUnique()
                .HasDatabaseName("UX_UserRoles_Tenant_User_Role_Store");

            // 用户ID索引
            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_UserRoles_UserId");

            // 角色ID索引
            builder.HasIndex(x => x.RoleId)
                .HasDatabaseName("IX_UserRoles_RoleId");

            builder.HasIndex(x => new { x.TenantId, x.RoleId })
                .HasDatabaseName("IX_UserRoles_TenantId_RoleId");

            // 租户+用户索引
            builder.HasIndex(x => new { x.TenantId, x.UserId })
                .HasDatabaseName("IX_UserRoles_TenantId_UserId");

            #endregion

            #region 外键关系

            builder.HasOne(x => x.User)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);

            builder.HasOne(x => x.Role)
                .WithMany(x => x.UserRoles)
                .HasForeignKey(x => x.RoleId)
                .OnDelete(DeleteBehavior.Restrict);

            #endregion
        }
    }

    // ========================================
    // UserLoginLog 实体配置（登录日志）
    // ========================================

    /// <summary>
    /// 用户登录日志配置
    /// </summary>
    public class UserLoginLogConfiguration : BaseEntityConfiguration<UserLoginLog>
    {
        public override void Configure(EntityTypeBuilder<UserLoginLog> builder)
        {
            base.Configure(builder);

            builder.ToTable("UserLoginLogs");

            builder.Property(x => x.TenantId).IsRequired();
            builder.Property(x => x.UserId).IsRequired();
            builder.Property(x => x.SessionId).HasMaxLength(32);
            builder.Property(x => x.TokenVersion).IsRequired().HasDefaultValue(1);
            builder.Property(x => x.IpAddress).IsRequired().HasMaxLength(50);
            builder.Property(x => x.UserAgent).HasMaxLength(500);
            builder.Property(x => x.DeviceType).HasMaxLength(20);
            builder.Property(x => x.Browser).HasMaxLength(50);
            builder.Property(x => x.OperatingSystem).HasMaxLength(50);
            builder.Property(x => x.LoginMethod).IsRequired().HasMaxLength(50).HasDefaultValue("Password");
            builder.Property(x => x.IsSuccess).IsRequired();
            builder.Property(x => x.FailureReason).HasMaxLength(500);
            builder.Property(x => x.LogoutType).HasMaxLength(50);

            // ✅ SessionId索引（用于关联业务日志）
            builder.HasIndex(x => x.SessionId)
                .HasDatabaseName("IX_UserLoginLogs_SessionId");

            // 用户索引
            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_UserLoginLogs_UserId");

            // 租户+用户索引
            builder.HasIndex(x => new { x.TenantId, x.UserId })
                .HasDatabaseName("IX_UserLoginLogs_TenantId_UserId");

            // 登录时间索引
            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_UserLoginLogs_CreatedAt");

            // 登录成功状态索引
            builder.HasIndex(x => new { x.UserId, x.IsSuccess, x.CreatedAt })
                .HasDatabaseName("IX_UserLoginLogs_UserId_IsSuccess_CreatedAt");

            // IP地址索引（安全审计）
            builder.HasIndex(x => new { x.IpAddress, x.CreatedAt })
                .HasDatabaseName("IX_UserLoginLogs_IpAddress_CreatedAt");

            // 外键
            builder.HasOne(x => x.User)
                .WithMany(x => x.LoginLogs)
                .HasForeignKey(x => x.UserId)
                .OnDelete(DeleteBehavior.Cascade);
        }
    }
}
