using VeriScan.Domain.Entities;

namespace VeriScan.Application.Abstractions;

/// <summary>关键管理操作的安全审计写入请求。</summary>
public sealed record AuditEntry(
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

/// <summary>审核 API 请求的计量事实。</summary>
public sealed record ApiRequestMeasurement(
    Guid? TenantId,
    Guid? ApplicationId,
    Guid? ApiKeyId,
    Guid? ModerationRequestId,
    string RouteTemplate,
    string AuthenticationOutcome,
    string IdempotencyOutcome,
    int HttpStatusCode,
    int? ItemCount,
    long? LatencyMilliseconds,
    DateTimeOffset OccurredAt);

/// <summary>一次 AI 调用的供应商返回和计量事实。</summary>
public sealed record AiInvocationMeasurement(
    Guid TenantId,
    Guid ApplicationId,
    Guid ApiKeyId,
    Guid ModerationRequestId,
    Guid ModerationItemId,
    string Outcome,
    string? ConfigurationRevision,
    string? ProviderRequestId,
    int AttemptNumber,
    int? InputTokens,
    int? OutputTokens,
    string? FailureCode,
    long? LatencyMilliseconds,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt);

/// <summary>与业务事实同库写入的 Outbox 事件。</summary>
public sealed record OutboxMessage(
    string EventType,
    string AggregateType,
    Guid AggregateId,
    Guid? TenantId,
    Guid? ApplicationId,
    string PayloadJson,
    DateTimeOffset OccurredAt);

/// <summary>将审计、请求、AI 和 Outbox 事实加入当前业务事务。</summary>
public interface IOperationalFactService
{
    Task RecordAuditAsync(AuditEntry entry, CancellationToken cancellationToken);

    Task RecordApiRequestAsync(
        ApiRequestMeasurement measurement,
        CancellationToken cancellationToken);

    Task RecordAiInvocationAsync(
        AiInvocationMeasurement measurement,
        CancellationToken cancellationToken);

    Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken);
}

/// <summary>事实持久化写入边界，方法本身不提交事务。</summary>
public interface IOperationalFactStore
{
    Task AddAuditAsync(AuditEvent auditEvent, CancellationToken cancellationToken);

    Task AddApiRequestAsync(ApiRequestEvent requestEvent, CancellationToken cancellationToken);

    Task AddAiInvocationAsync(AiInvocation invocation, CancellationToken cancellationToken);

    Task AddOutboxAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken);
}

/// <summary>Outbox 投递器读取和更新边界。</summary>
public interface IOutboxStore
{
    Task<IReadOnlyList<OutboxEvent>> ListAvailableAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken);

    /// <summary>在数据库事务中领取一批事件并写入租约。</summary>
    Task<IReadOnlyList<OutboxEvent>> ClaimAvailableAsync(
        DateTimeOffset now,
        int limit,
        TimeSpan leaseDuration,
        string lockToken,
        CancellationToken cancellationToken);

    /// <summary>校验租约令牌后，以消费账本和完成状态原子确认事件。</summary>
    Task<bool> TryCompleteAsync(
        Guid outboxEventId,
        string lockToken,
        string consumerName,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken);

    /// <summary>校验租约令牌后写入失败退避时间，事件仍保留待重试。</summary>
    Task<bool> TryFailAsync(
        Guid outboxEventId,
        string lockToken,
        string errorCode,
        DateTimeOffset availableAt,
        CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}
