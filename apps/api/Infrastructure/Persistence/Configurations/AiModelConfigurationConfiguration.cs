using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

public sealed class AiModelConfigurationConfiguration : IEntityTypeConfiguration<AiModelConfiguration>
{
    public void Configure(EntityTypeBuilder<AiModelConfiguration> builder)
    {
        builder.ToTable("ai_model_configurations");
        builder.HasKey(configuration => configuration.Id);
        builder.Property(configuration => configuration.PublicRevisionId).HasMaxLength(64).IsRequired();
        builder.Property(configuration => configuration.Name).HasMaxLength(100).IsRequired();
        builder.Property(configuration => configuration.Protocol).HasConversion<string>().HasMaxLength(48).IsRequired();
        builder.Property(configuration => configuration.BaseUrl).HasMaxLength(2048).IsRequired();
        builder.Property(configuration => configuration.EndpointPath).HasMaxLength(256).IsRequired();
        builder.Property(configuration => configuration.CredentialRef).HasMaxLength(256).IsRequired();
        builder.Property(configuration => configuration.AuthScheme).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(configuration => configuration.Model).HasMaxLength(200).IsRequired();
        builder.Property(configuration => configuration.ApiVersion).HasMaxLength(64);
        builder.Property(configuration => configuration.ApiVersionLocation).HasConversion<string>().HasMaxLength(16).IsRequired();
        builder.Property(configuration => configuration.SystemPrompt).HasColumnType("text").IsRequired();
        builder.Property(configuration => configuration.DecodingMode).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(configuration => configuration.DataRegion).HasMaxLength(100).IsRequired();
        builder.Property(configuration => configuration.RetentionClass).HasMaxLength(100).IsRequired();
        builder.Property(configuration => configuration.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(configuration => configuration.LastTestFailureCode).HasMaxLength(64);
        builder.Property(configuration => configuration.AdapterContractVersion).HasMaxLength(64);
        builder.Property(configuration => configuration.CanonicalSchemaVersion).HasMaxLength(64);
        builder.Property(configuration => configuration.CanonicalSchemaHash).HasMaxLength(64);
        builder.Property(configuration => configuration.EffectiveSchemaHash).HasMaxLength(64);
        builder.Property(configuration => configuration.SchemaTransformerVersion).HasMaxLength(64);
        builder.HasIndex(configuration => configuration.PublicRevisionId).IsUnique();
        builder.HasIndex(configuration => configuration.Name);
        builder.HasIndex(configuration => configuration.IsActive)
            .IsUnique()
            .HasFilter("\"IsActive\" = TRUE");
    }
}
