using Atlas.Core.Entities.Global;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Global.Migrations.EntityConfigurations
{
    /// <summary>
    /// 租户配置
    /// </summary>
    public class TenantConfiguration : IEntityTypeConfiguration<Tenant>
    {
        public void Configure(EntityTypeBuilder<Tenant> builder)
        {
            // 表名
            builder.ToTable("Tenants");

            // 主键
            builder.HasKey(e => e.Id);

            // 唯一索引
            builder.HasIndex(e => e.Domain)
                .IsUnique()
                .HasDatabaseName("UX_Tenants_Domain");

            // 普通索引
            builder.HasIndex(e => e.Status)
                .HasDatabaseName("IX_Tenants_Status");

            builder.HasIndex(e => e.DatabaseInstanceId)
                .HasDatabaseName("IX_Tenants_DatabaseInstanceId");

            builder.HasIndex(e => new { e.Status, e.TenantType })
                .HasDatabaseName("IX_Tenants_Status_TenantType");

            builder.HasIndex(e => e.City)
                .HasDatabaseName("IX_Tenants_City");

            builder.HasIndex(e => e.IsDeleted)
                .HasDatabaseName("IX_Tenants_IsDeleted");

            // 属性配置
            builder.Property(e => e.Name)
                .IsRequired()
                .HasMaxLength(200)
                .HasComment("公司名称");

            builder.Property(e => e.BrandName)
                .HasMaxLength(200)
                .HasComment("品牌名称");

            builder.Property(e => e.Address)
                .HasMaxLength(500)
                .HasComment("公司地址");

            builder.Property(e => e.PhoneNumber)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("公司电话");

            builder.Property(e => e.ContactName)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("联系人姓名");

            builder.Property(e => e.ContactPhoneNumber)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("联系人手机号");

            builder.Property(e => e.ContactEmail)
                .HasMaxLength(100)
                .HasComment("联系人邮箱");

            builder.Property(e => e.Domain)
                .IsRequired()
                .HasMaxLength(100)
                .HasComment("公司代码（租户唯一标识，用于登录）");

            builder.Property(e => e.TenantType)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasComment("租户类型：Enterprise, Individual");

            builder.Property(e => e.Province)
                .HasMaxLength(50)
                .HasComment("省份");

            builder.Property(e => e.City)
                .IsRequired()
                .HasMaxLength(50)
                .HasComment("城市");

            builder.Property(e => e.Category)
                .HasMaxLength(50)
                .HasComment("租户类别：试用、Mobile等");

            builder.Property(e => e.Status)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasComment("租户状态：Active, Inactive, Suspended");

            builder.Property(e => e.BusinessType)
                .IsRequired()
                .HasConversion<string>()
                .HasMaxLength(50)
                .HasComment("连锁类型：Single, Chain, Franchise");

            builder.Property(e => e.DatabaseInstanceId)
                .IsRequired()
                .HasComment("关联的数据库实例ID");

            builder.Property(e => e.OfficeCount)
                .IsRequired()
                .HasDefaultValue(0)
                .HasComment("诊所/门店数量");

            builder.Property(e => e.CreatedAt)
                .IsRequired()
                .HasComment("创建时间");

            builder.Property(e => e.UpdatedAt)
                .HasComment("更新时间");

            builder.Property(e => e.CreatedBy)
                .HasComment("创建人ID");

            builder.Property(e => e.UpdatedBy)
                .HasComment("更新人ID");

            builder.Property(e => e.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false)
                .HasComment("是否已删除（软删除）");

            builder.Property(e => e.DeletedAt)
                .HasComment("删除时间");

            builder.Property(e => e.DeletedBy)
                .HasComment("删除人ID");

            // 关系配置
            builder.HasOne(e => e.DatabaseInstance)
                .WithMany(d => d.Tenants)
                .HasForeignKey(e => e.DatabaseInstanceId)
                .OnDelete(DeleteBehavior.Restrict);

            // 查询过滤器（软删除）
            builder.HasQueryFilter(e => !e.IsDeleted);
        }
    }
}
