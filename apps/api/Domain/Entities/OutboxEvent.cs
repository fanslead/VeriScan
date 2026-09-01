namespace VeriScan.Domain.Entities;

/// <summary>与业务事实同事务写入的待投递事件。</summary>
public sealed class OutboxEvent
{
    private OutboxEvent()
    {
    }

    public OutboxEvent(
        string eventType,
        string aggregateType,
        Guid aggregateId,
        Guid? tenantId,
        Guid? applicationId,
        string payloadJson,
        DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        EventType = eventType;
        AggregateType = aggregateType;
        AggregateId = aggregateId;
        TenantId = tenantId;
        ApplicationId = applicationId;
        PayloadJson = payloadJson;
        OccurredAt = occurredAt;
        AvailableAt = occurredAt;
        CreatedAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    public string AggregateType { get; private set; } = string.Empty;

    public Guid AggregateId { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? ApplicationId { get; private set; }

    /// <summary>事件负载只允许包含标识、状态和计量事实，不得包含原文或凭证。</summary>
    public string PayloadJson { get; private set; } = string.Empty;

    public DateTimeOffset OccurredAt { get; private set; }

    public DateTimeOffset AvailableAt { get; private set; }

    public DateTimeOffset? PublishedAt { get; private set; }

    public DateTimeOffset? LockedUntil { get; private set; }

    public string? LockToken { get; private set; }

    public int AttemptCount { get; private set; }

    public string? LastErrorCode { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public bool IsPublished => PublishedAt.HasValue;

    public bool IsAvailable(DateTimeOffset now)
    {
        return !IsPublished &&
               AvailableAt <= now &&
               (LockedUntil is null || LockedUntil <= now);
    }

    public void Claim(string lockToken, DateTimeOffset lockedUntil)
    {
        if (IsPublished)
        {
            return;
        }

        LockToken = lockToken;
        LockedUntil = lockedUntil;
        AttemptCount++;
    }

    public void MarkPublished(DateTimeOffset publishedAt)
    {
        PublishedAt = publishedAt;
        LockedUntil = null;
        LockToken = null;
        LastErrorCode = null;
    }

    public void MarkFailed(string errorCode, DateTimeOffset availableAt)
    {
        LastErrorCode = errorCode;
        AvailableAt = availableAt;
        LockedUntil = null;
        LockToken = null;
    }
}
