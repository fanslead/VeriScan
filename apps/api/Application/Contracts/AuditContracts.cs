namespace VeriScan.Application.Contracts;

/// <summary>管理端审计事件查询参数。</summary>
public sealed record AuditEventQuery
{
    public Guid? ApplicationId { get; init; }

    public Guid? ApiKeyId { get; init; }

    public string? Action { get; init; }

    public DateTimeOffset? From { get; init; }

    public DateTimeOffset? Through { get; init; }

    public int? Limit { get; init; }
}

/// <summary>管理端审计事件安全响应。</summary>
public sealed record AuditEventResponse(
    Guid Id,
    Guid? TenantId,
    Guid? ApplicationId,
    Guid? ApiKeyId,
    string ActorType,
    string? ActorId,
    string Action,
    string ResourceType,
    string ResourceId,
    string? BeforeJson,
    string? AfterJson,
    string? CorrelationId,
    DateTimeOffset OccurredAt);

/// <summary>管理端审计事件分页响应。</summary>
public sealed record AuditEventListResponse(
    IReadOnlyList<AuditEventResponse> Items,
    long Total,
    DateTimeOffset DataFrom,
    DateTimeOffset DataThrough);
