using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

public sealed class ApiKeyConfiguration : IEntityTypeConfiguration<ApplicationApiKey>
{
    public void Configure(EntityTypeBuilder<ApplicationApiKey> builder)
    {
        builder.ToTable("application_api_keys");
        builder.HasKey(key => key.Id);
        builder.Property(key => key.PublicKeyId).HasMaxLength(64).IsRequired();
        builder.Property(key => key.KeyPrefix).HasMaxLength(80).IsRequired();
        builder.Property(key => key.LastFour).HasMaxLength(4).IsFixedLength().IsRequired();
        builder.Property(key => key.SecretDigest).HasColumnType("bytea").IsRequired();
        builder.Property(key => key.PepperVersion).HasMaxLength(32).IsRequired();
        builder.Property(key => key.ScopesText).HasMaxLength(512).IsRequired();
        builder.Property(key => key.DisplayName).HasMaxLength(100).IsRequired();
        builder.Property(key => key.EnvironmentName).HasMaxLength(16).IsRequired();
        builder.Property(key => key.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(key => key.PublicKeyId).IsUnique();
        builder.HasIndex(key => new { key.ApplicationId, key.Status });
    }
}
