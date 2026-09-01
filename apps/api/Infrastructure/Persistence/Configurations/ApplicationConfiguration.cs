using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

public sealed class ApplicationConfiguration : IEntityTypeConfiguration<ApplicationEntity>
{
    public void Configure(EntityTypeBuilder<ApplicationEntity> builder)
    {
        builder.ToTable("applications");
        builder.HasKey(application => application.Id);
        builder.Property(application => application.PublicId).HasMaxLength(80).IsRequired();
        builder.Property(application => application.Name).HasMaxLength(100).IsRequired();
        builder.Property(application => application.EnvironmentName).HasMaxLength(16).IsRequired();
        builder.Property(application => application.Status).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.HasIndex(application => new { application.TenantId, application.PublicId }).IsUnique();
        builder.HasIndex(application => application.RuleSetVersionId);
        builder.HasMany(application => application.ApiKeys)
            .WithOne(key => key.Application)
            .HasForeignKey(key => key.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.HasMany(application => application.ModerationRequests)
            .WithOne(request => request.Application)
            .HasForeignKey(request => request.ApplicationId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
