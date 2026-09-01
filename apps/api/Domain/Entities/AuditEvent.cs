namespace VeriScan.Domain.Entities;

/// <summary>记录管理操作的不可变审计事实。</summary>
public sealed class AuditEvent
{
    private AuditEvent()
    {
    }

    public AuditEvent(
        Guid? tenantId,
        Guid? applicationId,
        Guid? apiKeyId,
        string actorType,
        string? actorId,
        string action,
        string resourceType,
        string resourceId,
        string? beforeJson,
        string? afterJson,
        string? correlationId,
        DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        ApiKeyId = apiKeyId;
        ActorType = actorType;
        ActorId = actorId;
        Action = action;
        ResourceType = resourceType;
        ResourceId = resourceId;
        BeforeJson = beforeJson;
        AfterJson = afterJson;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? ApplicationId { get; private set; }

    public Guid? ApiKeyId { get; private set; }

    public string ActorType { get; private set; } = string.Empty;

    public string? ActorId { get; private set; }

    public string Action { get; private set; } = string.Empty;

    public string ResourceType { get; private set; } = string.Empty;

    public string ResourceId { get; private set; } = string.Empty;

    /// <summary>变更前的安全摘要，只允许写入非敏感字段。</summary>
    public string? BeforeJson { get; private set; }

    /// <summary>变更后的安全摘要，只允许写入非敏感字段。</summary>
    public string? AfterJson { get; private set; }

    public string? CorrelationId { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
