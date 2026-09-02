using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

/// <summary>配置应用唯一 Webhook 目标及当前版本的测试门禁。</summary>
public sealed class ApplicationWebhookConfiguration : IEntityTypeConfiguration<ApplicationWebhook>
{
    public void Configure(EntityTypeBuilder<ApplicationWebhook> builder)
    {
        builder.ToTable("application_webhooks");
        builder.HasKey(webhook => webhook.Id);
        builder.Property(webhook => webhook.EndpointUrl).HasMaxLength(2048).IsRequired();
        builder.Property(webhook => webhook.ProviderApplicationId).HasMaxLength(128).IsRequired();
        builder.Property(webhook => webhook.ProviderEndpointId).HasMaxLength(128).IsRequired();
        builder.Property(webhook => webhook.LastTestOutcome)
            .HasConversion<string>()
            .HasMaxLength(32);
        builder.Property(webhook => webhook.UpdatedAt).IsConcurrencyToken();
        builder.HasIndex(webhook => webhook.ApplicationId).IsUnique();
        builder.HasOne<ApplicationEntity>()
            .WithOne()
            .HasForeignKey<ApplicationWebhook>(webhook => webhook.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>配置提交到 Webhook 供应商前的可靠发布队列。</summary>
public sealed class WebhookPublicationConfiguration : IEntityTypeConfiguration<WebhookPublication>
{
    public void Configure(EntityTypeBuilder<WebhookPublication> builder)
    {
        builder.ToTable("webhook_publications");
        builder.HasKey(publication => publication.Id);
        builder.Property(publication => publication.ProviderApplicationId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(publication => publication.ProviderEndpointId)
            .HasMaxLength(128)
            .IsRequired();
        builder.Property(publication => publication.Kind)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(publication => publication.EventType).HasMaxLength(96).IsRequired();
        builder.Property(publication => publication.PayloadJson).HasColumnType("jsonb").IsRequired();
        builder.Property(publication => publication.DeduplicationKey)
            .HasMaxLength(160)
            .IsRequired();
        builder.Property(publication => publication.Status)
            .HasConversion<string>()
            .HasMaxLength(32)
            .IsRequired();
        builder.Property(publication => publication.LeaseOwner).HasMaxLength(128);
        builder.Property(publication => publication.ProviderMessageId).HasMaxLength(128);
        builder.Property(publication => publication.LastErrorCode).HasMaxLength(96);
        builder.HasIndex(publication => publication.DeduplicationKey).IsUnique();
        builder.HasIndex(publication => new
        {
            publication.Status,
            publication.AvailableAt,
            publication.CreatedAt
        });
        builder.HasOne<ApplicationWebhook>()
            .WithMany()
            .HasForeignKey(publication => publication.ApplicationWebhookId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
