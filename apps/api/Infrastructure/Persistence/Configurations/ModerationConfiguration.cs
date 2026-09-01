using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

public sealed class ModerationRequestConfiguration : IEntityTypeConfiguration<ModerationRequest>
{
    public void Configure(EntityTypeBuilder<ModerationRequest> builder)
    {
        builder.ToTable("moderation_requests");
        builder.HasKey(request => request.Id);
        builder.Property(request => request.Mode).HasMaxLength(16).IsRequired();
        builder.Property(request => request.IdempotencyKeyDigest).HasMaxLength(128);
        builder.Property(request => request.RequestFingerprint).HasMaxLength(128);
        builder.Property(request => request.ProcessingStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(request => new { request.ApplicationId, request.SubmittedAt });
        builder.HasIndex(request => new { request.ApplicationId, request.IdempotencyKeyDigest })
            .IsUnique()
            .HasFilter("\"IdempotencyKeyDigest\" IS NOT NULL");
        builder.HasMany(request => request.Items)
            .WithOne(item => item.Request)
            .HasForeignKey(item => item.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}

public sealed class ModerationItemConfiguration : IEntityTypeConfiguration<ModerationItem>
{
    public void Configure(EntityTypeBuilder<ModerationItem> builder)
    {
        builder.ToTable("moderation_items");
        builder.HasKey(item => item.Id);
        builder.Property(item => item.ClientItemId).HasMaxLength(128).IsRequired();
        builder.Property(item => item.Content).HasColumnType("text").IsRequired();
        builder.Property(item => item.ContentHash).HasMaxLength(64).IsRequired();
        builder.Property(item => item.Language).HasMaxLength(32);
        builder.Property(item => item.ContentType).HasMaxLength(32).IsRequired();
        builder.Property(item => item.ProcessingStatus).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(item => item.Decision).HasConversion<string>().HasMaxLength(32);
        builder.Property(item => item.ReviewSource).HasMaxLength(64);
        builder.Property(item => item.Route).HasMaxLength(64).IsRequired();
        builder.Property(item => item.ReasonCodesText).HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.CategoriesText).HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.EvidenceText).HasColumnType("jsonb").IsRequired();
        builder.Property(item => item.AiConfigurationRevision).HasMaxLength(64);
        builder.Property(item => item.ProviderRequestId).HasMaxLength(256);
        builder.Property(item => item.AiFailureCode).HasMaxLength(64);
        builder.Property(item => item.RiskScore).HasPrecision(6, 5);
        builder.HasIndex(item => new { item.ApplicationId, item.CreatedAt });
        builder.HasIndex(item => new { item.RequestId, item.ClientItemId }).IsUnique();
    }
}
