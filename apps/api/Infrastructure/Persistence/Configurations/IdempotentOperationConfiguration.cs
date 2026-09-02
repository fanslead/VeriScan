using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

/// <summary>配置关键写操作的幂等结果与唯一作用域。</summary>
public sealed class IdempotentOperationConfiguration : IEntityTypeConfiguration<IdempotentOperation>
{
    public void Configure(EntityTypeBuilder<IdempotentOperation> builder)
    {
        builder.ToTable("idempotent_operations");
        builder.HasKey(operation => operation.Id);
        builder.Property(operation => operation.Operation).HasMaxLength(48).IsRequired();
        builder.Property(operation => operation.IdempotencyKeyDigest).HasMaxLength(128).IsRequired();
        builder.Property(operation => operation.OperationFingerprint).HasMaxLength(128).IsRequired();
        builder.Property(operation => operation.ResponseSnapshot).HasColumnType("jsonb").IsRequired();
        builder.HasIndex(operation => new
        {
            operation.ApplicationId,
            operation.TargetRequestId,
            operation.Operation,
            operation.IdempotencyKeyDigest
        }).IsUnique();
        builder.HasIndex(operation => operation.ExpiresAt);
    }
}
