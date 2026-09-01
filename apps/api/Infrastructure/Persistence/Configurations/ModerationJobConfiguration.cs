using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

public sealed class ModerationJobConfiguration : IEntityTypeConfiguration<ModerationJob>
{
    public void Configure(EntityTypeBuilder<ModerationJob> builder)
    {
        builder.ToTable("moderation_jobs");
        builder.HasKey(job => job.Id);
        builder.Property(job => job.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(job => job.LeaseOwner).HasMaxLength(128);
        builder.Property(job => job.LastErrorCode).HasMaxLength(64);
        builder.HasIndex(job => job.RequestId).IsUnique();
        builder.HasIndex(job => new { job.Status, job.AvailableAt, job.Priority });
        builder.HasOne(job => job.Request)
            .WithOne()
            .HasForeignKey<ModerationJob>(job => job.RequestId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
