using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

public sealed class WordRuleConfiguration : IEntityTypeConfiguration<WordRule>
{
    public void Configure(EntityTypeBuilder<WordRule> builder)
    {
        builder.ToTable("word_rules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Term).HasMaxLength(200).IsRequired();
        builder.Property(rule => rule.Type).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(rule => rule.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(rule => rule.MatchMode).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(rule => rule.Category).HasMaxLength(64).IsRequired();
        builder.Property(rule => rule.Weight).HasPrecision(5, 4);
        builder.Property(rule => rule.Language).HasMaxLength(32);
        builder.Property(rule => rule.Scene).HasMaxLength(64);
        builder.Property(rule => rule.EvidenceTemplate).HasMaxLength(256);
        builder.Property(rule => rule.Source).HasMaxLength(128);
        builder.HasIndex(rule => new { rule.RuleSetVersionId, rule.IsEnabled, rule.Type });
        builder.HasIndex(rule => new { rule.RuleSetVersionId, rule.IsEnabled, rule.Action });
        builder.HasIndex(rule => new { rule.RuleSetVersionId, rule.Term });
    }
}
