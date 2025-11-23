using Atlas.Core.Entities.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Atlas.Data.Tenant.Migrations.EntityConfigurations
{
    public class OperationLogConfiguration : BaseEntityConfiguration<OperationLog>
    {
        public override void Configure(EntityTypeBuilder<OperationLog> builder)
        {
            base.Configure(builder);

            builder.ToTable("OperationLogs");

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.UserId)
                .IsRequired(false);

            builder.Property(x => x.StoreId)
                .IsRequired(false);

            builder.Property(x => x.SessionId)
                .HasMaxLength(32);

            builder.Property(x => x.Module)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.OperationType)
                .IsRequired()
                .HasMaxLength(50);

            builder.Property(x => x.Description)
                .IsRequired()
                .HasMaxLength(500);

            builder.Property(x => x.EntityId)
                .IsRequired(false);

            builder.Property(x => x.Changes)
                .HasMaxLength(4000);

            builder.Property(x => x.IpAddress)
                .HasMaxLength(50);

            builder.Property(x => x.IsSuccess)
                .IsRequired()
                .HasDefaultValue(true);

            builder.Property(x => x.ErrorMessage)
                .HasMaxLength(1000);

            // SessionId索引
            builder.HasIndex(x => x.SessionId)
                .HasDatabaseName("IX_BusinessOperationLogs_SessionId")
                .HasFilter("[SessionId] IS NOT NULL");

            // 用户索引
            builder.HasIndex(x => x.UserId)
                .HasDatabaseName("IX_BusinessOperationLogs_UserId");

            // 租户+用户索引
            builder.HasIndex(x => new { x.TenantId, x.UserId })
                .HasDatabaseName("IX_BusinessOperationLogs_TenantId_UserId");

            // 模块+操作类型索引
            builder.HasIndex(x => new { x.Module, x.OperationType, x.CreatedAt })
                .HasDatabaseName("IX_BusinessOperationLogs_Module_Type_CreatedAt");

            // 业务实体索引
            builder.HasIndex(x => new { x.Module, x.EntityId })
                .HasDatabaseName("IX_BusinessOperationLogs_Module_EntityId")
                .HasFilter("[EntityId] IS NOT NULL");

            // 时间索引
            builder.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("IX_BusinessOperationLogs_CreatedAt");
        }
    }
}
