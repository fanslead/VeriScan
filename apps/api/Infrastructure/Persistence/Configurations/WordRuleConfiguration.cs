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
        builder.Property(rule => rule.Category).HasMaxLength(64).IsRequired();
        builder.Property(rule => rule.Weight).HasPrecision(5, 4);
        builder.HasIndex(rule => new { rule.RuleSetVersionId, rule.IsEnabled, rule.Type });
        builder.HasIndex(rule => new { rule.RuleSetVersionId, rule.Term });
    }
}
