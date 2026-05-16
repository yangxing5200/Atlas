using Atlas.Core.Entities.Tenant;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Atlas.Data.Tenant.Migrations.EntityConfigurations;

public sealed class TenantOutboxMessageConfiguration : BaseEntityConfiguration<TenantOutboxMessage>
{
    public override void Configure(EntityTypeBuilder<TenantOutboxMessage> builder)
    {
        base.Configure(builder);

        builder.ToTable("TenantOutboxMessages");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.StoreId)
            .IsRequired(false);

        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.EventName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.MessageType)
            .IsRequired()
            .HasMaxLength(500)
            .HasColumnType("varchar(500)");

        builder.Property(x => x.Payload)
            .IsRequired()
            .HasColumnType("longtext");

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired();

        builder.Property(x => x.AvailableAtUtc)
            .IsRequired(false);

        builder.Property(x => x.ProcessingAtUtc)
            .IsRequired(false);

        builder.Property(x => x.ProcessingBy)
            .HasMaxLength(100);

        builder.Property(x => x.ProcessedAtUtc)
            .IsRequired(false);

        builder.Property(x => x.AttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.NextAttemptAtUtc)
            .IsRequired(false);

        builder.Property(x => x.LastError)
            .HasMaxLength(2000)
            .HasColumnType("varchar(2000)");

        builder.HasIndex(x => x.EventId)
            .IsUnique()
            .HasDatabaseName("IX_TenantOutboxMessages_EventId");

        builder.HasIndex(x => new { x.TenantId, x.ProcessedAtUtc, x.NextAttemptAtUtc })
            .HasDatabaseName("IX_TenantOutboxMessages_Tenant_ProcessDue");

        builder.HasIndex(x => new { x.ProcessingAtUtc, x.ProcessedAtUtc })
            .HasDatabaseName("IX_TenantOutboxMessages_Processing");
    }
}

public sealed class TenantInboxMessageConfiguration : BaseEntityConfiguration<TenantInboxMessage>
{
    public override void Configure(EntityTypeBuilder<TenantInboxMessage> builder)
    {
        base.Configure(builder);

        builder.ToTable("TenantInboxMessages");

        builder.Property(x => x.TenantId)
            .IsRequired();

        builder.Property(x => x.MessageId)
            .IsRequired();

        builder.Property(x => x.ConsumerName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ReceivedAtUtc)
            .IsRequired();

        builder.HasIndex(x => new { x.MessageId, x.ConsumerName })
            .IsUnique()
            .HasDatabaseName("UX_TenantInboxMessages_Message_Consumer");

        builder.HasIndex(x => new { x.TenantId, x.ReceivedAtUtc })
            .HasDatabaseName("IX_TenantInboxMessages_Tenant_ReceivedAt");
    }
}
