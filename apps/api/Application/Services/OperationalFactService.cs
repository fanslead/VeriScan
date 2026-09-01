using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

/// <summary>构造并写入安全运营事实，不负责提交外层业务事务。</summary>
public sealed class OperationalFactService(IOperationalFactStore store) : IOperationalFactService
{
    public Task RecordAuditAsync(AuditEntry entry, CancellationToken cancellationToken)
    {
        var auditEvent = new AuditEvent(
            Normalize(entry.TenantId),
            Normalize(entry.ApplicationId),
            Normalize(entry.ApiKeyId),
            RequireShort(entry.ActorType, nameof(entry.ActorType), 32),
            NormalizeText(entry.ActorId, 256),
            RequireShort(entry.Action, nameof(entry.Action), 96),
            RequireShort(entry.ResourceType, nameof(entry.ResourceType), 64),
            RequireShort(entry.ResourceId, nameof(entry.ResourceId), 128),
            entry.BeforeJson,
            entry.AfterJson,
            NormalizeText(entry.CorrelationId, 128),
            entry.OccurredAt.ToUniversalTime());
        return store.AddAuditAsync(auditEvent, cancellationToken);
    }

    public Task RecordApiRequestAsync(
        ApiRequestMeasurement measurement,
        CancellationToken cancellationToken)
    {
        var requestEvent = new ApiRequestEvent(
            Normalize(measurement.TenantId),
            Normalize(measurement.ApplicationId),
            Normalize(measurement.ApiKeyId),
            Normalize(measurement.ModerationRequestId),
            RequireShort(measurement.RouteTemplate, nameof(measurement.RouteTemplate), 256),
            RequireShort(
                measurement.AuthenticationOutcome,
                nameof(measurement.AuthenticationOutcome),
                32),
            RequireShort(
                measurement.IdempotencyOutcome,
                nameof(measurement.IdempotencyOutcome),
                32),
            measurement.HttpStatusCode,
            measurement.ItemCount,
            measurement.LatencyMilliseconds,
            measurement.OccurredAt.ToUniversalTime());
        return store.AddApiRequestAsync(requestEvent, cancellationToken);
    }

    public Task RecordAiInvocationAsync(
        AiInvocationMeasurement measurement,
        CancellationToken cancellationToken)
    {
        var invocation = new AiInvocation(
            measurement.TenantId,
            measurement.ApplicationId,
            measurement.ApiKeyId,
            measurement.ModerationRequestId,
            measurement.ModerationItemId,
            RequireShort(measurement.Outcome, nameof(measurement.Outcome), 48),
            NormalizeText(measurement.ConfigurationRevision, 128),
            NormalizeText(measurement.ProviderRequestId, 256),
            measurement.AttemptNumber,
            measurement.InputTokens,
            measurement.OutputTokens,
            NormalizeText(measurement.FailureCode, 96),
            measurement.LatencyMilliseconds,
            measurement.StartedAt.ToUniversalTime(),
            measurement.CompletedAt.ToUniversalTime());
        return store.AddAiInvocationAsync(invocation, cancellationToken);
    }

    public Task EnqueueAsync(OutboxMessage message, CancellationToken cancellationToken)
    {
        var outboxEvent = new OutboxEvent(
            RequireShort(message.EventType, nameof(message.EventType), 96),
            RequireShort(message.AggregateType, nameof(message.AggregateType), 64),
            message.AggregateId,
            Normalize(message.TenantId),
            Normalize(message.ApplicationId),
            message.PayloadJson,
            message.OccurredAt.ToUniversalTime());
        return store.AddOutboxAsync(outboxEvent, cancellationToken);
    }

    private static Guid? Normalize(Guid? value)
    {
        return value is { } id && id != Guid.Empty ? id : null;
    }

    private static string? NormalizeText(string? value, int maximumLength)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return value.Trim().Length <= maximumLength
            ? value.Trim()
            : throw new RequestValidationException("运营事实字段长度超过限制。");
    }

    private static string RequireShort(string value, string fieldName, int maximumLength)
    {
        var normalized = NormalizeText(value, maximumLength);
        return normalized ?? throw new RequestValidationException($"{fieldName} 不能为空。");
    }
}
