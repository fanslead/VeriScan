using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

public sealed class RegexRuleConfiguration : IEntityTypeConfiguration<RegexRule>
{
    public void Configure(EntityTypeBuilder<RegexRule> builder)
    {
        builder.ToTable("regex_rules");
        builder.HasKey(rule => rule.Id);
        builder.Property(rule => rule.Pattern).HasMaxLength(RegexRuleSafetyValidatorMaximums.PatternLength).IsRequired();
        builder.Property(rule => rule.Action).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(rule => rule.Category).HasMaxLength(64).IsRequired();
        builder.Property(rule => rule.Weight).HasPrecision(5, 4);
        builder.Property(rule => rule.EngineMode).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(rule => rule.Language).HasMaxLength(32);
        builder.Property(rule => rule.Scene).HasMaxLength(64);
        builder.Property(rule => rule.EvidenceTemplate).HasMaxLength(256);
        builder.Property(rule => rule.Source).HasMaxLength(128);
        builder.HasIndex(rule => new { rule.RuleSetVersionId, rule.IsEnabled, rule.Priority });
        builder.HasIndex(rule => new { rule.RuleSetVersionId, rule.Pattern, rule.Category });
    }

    private static class RegexRuleSafetyValidatorMaximums
    {
        public const int PatternLength = 2_048;
    }
}
