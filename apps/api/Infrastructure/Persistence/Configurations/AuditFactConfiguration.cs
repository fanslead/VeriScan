using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Configurations;

/// <summary>配置审计事实的长度、索引和安全摘要字段。</summary>
public sealed class AuditEventConfiguration : IEntityTypeConfiguration<AuditEvent>
{
    public void Configure(EntityTypeBuilder<AuditEvent> builder)
    {
        builder.ToTable("audit_events");
        builder.HasKey(auditEvent => auditEvent.Id);
        builder.Property(auditEvent => auditEvent.ActorType).HasMaxLength(32).IsRequired();
        builder.Property(auditEvent => auditEvent.ActorId).HasMaxLength(256);
        builder.Property(auditEvent => auditEvent.Action).HasMaxLength(96).IsRequired();
        builder.Property(auditEvent => auditEvent.ResourceType).HasMaxLength(64).IsRequired();
        builder.Property(auditEvent => auditEvent.ResourceId).HasMaxLength(128).IsRequired();
        builder.Property(auditEvent => auditEvent.BeforeJson).HasColumnType("jsonb");
        builder.Property(auditEvent => auditEvent.AfterJson).HasColumnType("jsonb");
        builder.Property(auditEvent => auditEvent.CorrelationId).HasMaxLength(128);
        builder.HasIndex(auditEvent => new { auditEvent.ApplicationId, auditEvent.OccurredAt });
        builder.HasIndex(auditEvent => new { auditEvent.ActorId, auditEvent.OccurredAt });
    }
}

/// <summary>配置请求入口事实，支持按应用、Key 和时间窗口重建用量。</summary>
public sealed class ApiRequestEventConfiguration : IEntityTypeConfiguration<ApiRequestEvent>
{
    public void Configure(EntityTypeBuilder<ApiRequestEvent> builder)
    {
        builder.ToTable("api_request_events");
        builder.HasKey(requestEvent => requestEvent.Id);
        builder.Property(requestEvent => requestEvent.RouteTemplate).HasMaxLength(256).IsRequired();
        builder.Property(requestEvent => requestEvent.AuthenticationOutcome).HasMaxLength(32).IsRequired();
        builder.Property(requestEvent => requestEvent.IdempotencyOutcome).HasMaxLength(32).IsRequired();
        builder.HasIndex(requestEvent => new { requestEvent.ApplicationId, requestEvent.OccurredAt });
        builder.HasIndex(requestEvent => new { requestEvent.ApiKeyId, requestEvent.OccurredAt });
        builder.HasIndex(requestEvent => requestEvent.ModerationRequestId);
    }
}

/// <summary>配置 AI 调用事实和供应商返回的计量字段。</summary>
public sealed class AiInvocationConfiguration : IEntityTypeConfiguration<AiInvocation>
{
    public void Configure(EntityTypeBuilder<AiInvocation> builder)
    {
        builder.ToTable("ai_invocations");
        builder.HasKey(invocation => invocation.Id);
        builder.Property(invocation => invocation.Outcome).HasMaxLength(48).IsRequired();
        builder.Property(invocation => invocation.ConfigurationRevision).HasMaxLength(128);
        builder.Property(invocation => invocation.ProviderRequestId).HasMaxLength(256);
        builder.Property(invocation => invocation.FailureCode).HasMaxLength(96);
        builder.HasIndex(invocation => new { invocation.ApplicationId, invocation.CompletedAt });
        builder.HasIndex(invocation => new { invocation.ApiKeyId, invocation.CompletedAt });
        builder.HasIndex(invocation => invocation.ModerationItemId);
        builder.HasIndex(invocation => new
        {
            invocation.ModerationItemId,
            invocation.AttemptNumber
        }).IsUnique();
    }
}
