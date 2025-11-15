using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Atlas.Core.Entities;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore;

namespace Atlas.Data.Tenant.Migrations.EntityConfigurations
{
    public abstract class BaseEntityConfiguration<TEntity> : IEntityTypeConfiguration<TEntity>
          where TEntity : class, IBaseEntity
    {
        public virtual void Configure(EntityTypeBuilder<TEntity> builder)
        {
            builder.HasKey(x => x.Id);

            builder.Property(x => x.Id)
                .ValueGeneratedNever();

            builder.Property(x => x.CreatedAt)
                .IsRequired();

            builder.Property(x => x.UpdatedAt)
                .IsRequired(false);
        }
    }

    public abstract class AuditableEntityConfiguration<TEntity> : BaseEntityConfiguration<TEntity>
        where TEntity : class, IAuditable, ISoftDelete, IBaseEntity
    {
        public override void Configure(EntityTypeBuilder<TEntity> builder)
        {
            base.Configure(builder);

            builder.Property(x => x.CreatedBy)
                .IsRequired(false);

            builder.Property(x => x.UpdatedBy)
                .IsRequired(false);

            builder.Property(x => x.IsDeleted)
                .IsRequired()
                .HasDefaultValue(false);

            builder.Property(x => x.DeletedAt)
                .IsRequired(false);

            builder.Property(x => x.DeletedBy)
                .IsRequired(false);

            builder.HasIndex(nameof(ISoftDelete.IsDeleted))
                .HasDatabaseName($"IX_{typeof(TEntity).Name}s_IsDeleted");

            builder.HasQueryFilter(x => !x.IsDeleted);
        }
    }

    public abstract class VersionedEntityConfiguration<TEntity> : AuditableEntityConfiguration<TEntity>
        where TEntity : class, IVersioned, IAuditable, ISoftDelete, IBaseEntity
    {
        public override void Configure(EntityTypeBuilder<TEntity> builder)
        {
            base.Configure(builder);

            builder.Property(x => x.Version)
                .IsRequired()
                .HasDefaultValue(1)
                .IsConcurrencyToken();
        }
    }

    public abstract class SharedEntityConfiguration<TEntity> : VersionedEntityConfiguration<TEntity>
        where TEntity : class, ISharedEntity, IVersioned, IAuditable, ISoftDelete, IBaseEntity
    {
        public override void Configure(EntityTypeBuilder<TEntity> builder)
        {
            base.Configure(builder);

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.StoreId)
                .IsRequired();

            var entityName = typeof(TEntity).Name;

            builder.HasIndex(x => new { x.TenantId, x.StoreId })
                .HasDatabaseName($"IX_{entityName}s_TenantId_StoreId");

            builder.HasIndex(x => new { x.TenantId, x.IsDeleted })
                .HasDatabaseName($"IX_{entityName}s_TenantId_IsDeleted");
        }
    }

    public abstract class StoreOnlyEntityConfiguration<TEntity> : AuditableEntityConfiguration<TEntity>
        where TEntity : class, IStoreOnlyEntity, IAuditable, ISoftDelete, IBaseEntity
    {
        public override void Configure(EntityTypeBuilder<TEntity> builder)
        {
            base.Configure(builder);

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.StoreId)
                .IsRequired();

            var entityName = typeof(TEntity).Name;

            builder.HasIndex(x => new { x.TenantId, x.StoreId })
                .HasDatabaseName($"IX_{entityName}s_TenantId_StoreId");

            builder.HasIndex(x => new { x.TenantId, x.IsDeleted })
                .HasDatabaseName($"IX_{entityName}s_TenantId_IsDeleted");
        }
    }

    public abstract class StoreOnlyVersionedEntityConfiguration<TEntity> : VersionedEntityConfiguration<TEntity>
        where TEntity : class, IStoreOnlyEntity, IVersioned, IAuditable, ISoftDelete, IBaseEntity
    {
        public override void Configure(EntityTypeBuilder<TEntity> builder)
        {
            base.Configure(builder);

            builder.Property(x => x.TenantId)
                .IsRequired();

            builder.Property(x => x.StoreId)
                .IsRequired();

            var entityName = typeof(TEntity).Name;

            builder.HasIndex(x => new { x.TenantId, x.StoreId })
                .HasDatabaseName($"IX_{entityName}s_TenantId_StoreId");

            builder.HasIndex(x => new { x.TenantId, x.IsDeleted })
                .HasDatabaseName($"IX_{entityName}s_TenantId_IsDeleted");
        }
    }
}
