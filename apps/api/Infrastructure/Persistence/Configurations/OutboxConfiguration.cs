using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

/// <summary>配置同库 Outbox 事件和投递状态。</summary>
public sealed class OutboxEventConfiguration : IEntityTypeConfiguration<OutboxEvent>
{
    public void Configure(EntityTypeBuilder<OutboxEvent> builder)
    {
        builder.ToTable("outbox_events");
        builder.HasKey(outboxEvent => outboxEvent.Id);
        builder.Property(outboxEvent => outboxEvent.EventType).HasMaxLength(96).IsRequired();
        builder.Property(outboxEvent => outboxEvent.AggregateType).HasMaxLength(64).IsRequired();
        builder.Property(outboxEvent => outboxEvent.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(outboxEvent => outboxEvent.LockToken).HasMaxLength(128);
        builder.Property(outboxEvent => outboxEvent.LastErrorCode).HasMaxLength(96);
        builder.HasIndex(outboxEvent => new
        {
            outboxEvent.PublishedAt,
            outboxEvent.AvailableAt,
            outboxEvent.OccurredAt
        });
        builder.HasIndex(outboxEvent => new
        {
            outboxEvent.ApplicationId,
            outboxEvent.OccurredAt
        });
    }
}
