namespace VeriScan.Application.Abstractions;

/// <summary>审计事件只读查询参数。</summary>
public sealed record AuditReadQuery(
    Guid? ApplicationId,
    Guid? ApiKeyId,
    string? Action,
    DateTimeOffset From,
    DateTimeOffset Through,
    int Limit);

/// <summary>审计事件安全读取结果。</summary>
public sealed record AuditReadData(
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

/// <summary>审计事件只读存储边界。</summary>
public interface IAuditReadStore
{
    Task<bool> ApplicationExistsAsync(Guid applicationId, CancellationToken cancellationToken);

    Task<(IReadOnlyList<AuditReadData> Items, long Total)> ListAsync(
        AuditReadQuery query,
        CancellationToken cancellationToken);
}
