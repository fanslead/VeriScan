using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

public sealed class RuleSetVersionConfiguration : IEntityTypeConfiguration<RuleSetVersion>
{
    public void Configure(EntityTypeBuilder<RuleSetVersion> builder)
    {
        builder.ToTable("rule_set_versions");
        builder.HasKey(ruleSet => ruleSet.Id);
        builder.Property(ruleSet => ruleSet.PublicRevisionId).HasMaxLength(80).IsRequired();
        builder.Property(ruleSet => ruleSet.Name).HasMaxLength(100).IsRequired();
        builder.Property(ruleSet => ruleSet.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(ruleSet => ruleSet.UpdatedAt).IsConcurrencyToken();
        builder.Property(ruleSet => ruleSet.LastValidatedChecksum).HasMaxLength(64);
        builder.Property(ruleSet => ruleSet.PublishedChecksum).HasMaxLength(64);
        builder.HasIndex(ruleSet => ruleSet.PublicRevisionId).IsUnique();
        builder.HasIndex(ruleSet => new { ruleSet.Status, ruleSet.UpdatedAt });
        builder.HasMany(ruleSet => ruleSet.Rules)
            .WithOne(rule => rule.RuleSetVersion)
            .HasForeignKey(rule => rule.RuleSetVersionId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.HasMany(ruleSet => ruleSet.Applications)
            .WithOne(application => application.RuleSetVersion)
            .HasForeignKey(application => application.RuleSetVersionId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
