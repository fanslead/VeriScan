using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

/// <summary>配置小时用量投影。</summary>
public sealed class UsageHourlyConfiguration : IEntityTypeConfiguration<UsageHourly>
{
    public void Configure(EntityTypeBuilder<UsageHourly> builder)
    {
        builder.ToTable("usage_hourly");
        builder.HasKey(usage => usage.Id);
        builder.HasIndex(usage => new
        {
            usage.ApplicationId,
            usage.ApiKeyId,
            usage.BucketStart
        }).IsUnique();
        builder.HasIndex(usage => new { usage.ApplicationId, usage.BucketStart });
    }
}

/// <summary>配置日用量投影。</summary>
public sealed class UsageDailyConfiguration : IEntityTypeConfiguration<UsageDaily>
{
    public void Configure(EntityTypeBuilder<UsageDaily> builder)
    {
        builder.ToTable("usage_daily");
        builder.HasKey(usage => usage.Id);
        builder.HasIndex(usage => new
        {
            usage.ApplicationId,
            usage.ApiKeyId,
            usage.BucketStart
        }).IsUnique();
        builder.HasIndex(usage => new { usage.ApplicationId, usage.BucketStart });
    }
}

/// <summary>配置用量投影的 Outbox 消费去重账本。</summary>
public sealed class UsageConsumedEventConfiguration : IEntityTypeConfiguration<UsageConsumedEvent>
{
    public void Configure(EntityTypeBuilder<UsageConsumedEvent> builder)
    {
        builder.ToTable("usage_consumed_events");
        builder.HasKey(consumedEvent => consumedEvent.Id);
        builder.Property(consumedEvent => consumedEvent.ConsumerName).HasMaxLength(96).IsRequired();
        builder.HasIndex(consumedEvent => new
        {
            consumedEvent.ConsumerName,
            consumedEvent.OutboxEventId
        }).IsUnique();
    }
}
