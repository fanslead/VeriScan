namespace VeriScan.Domain.Entities;

/// <summary>按应用、API Key 和小时保存的可重建用量投影。</summary>
public sealed class UsageHourly
{
    private UsageHourly()
    {
    }

    public UsageHourly(
        Guid tenantId,
        Guid applicationId,
        Guid apiKeyId,
        DateTimeOffset bucketStart)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        ApiKeyId = apiKeyId;
        BucketStart = bucketStart;
        CreatedAt = bucketStart;
        UpdatedAt = bucketStart;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid ApiKeyId { get; private set; }

    public DateTimeOffset BucketStart { get; private set; }

    public long RequestCount { get; private set; }

    public long IdempotencyReplayCount { get; private set; }

    public long ItemCount { get; private set; }

    public long PassCount { get; private set; }

    public long RejectCount { get; private set; }

    public long ReviewCount { get; private set; }

    public long AiCallCount { get; private set; }

    public long AiFailureCount { get; private set; }

    public long? InputTokens { get; private set; }

    public long? OutputTokens { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Replace(
        long requestCount,
        long idempotencyReplayCount,
        long itemCount,
        long passCount,
        long rejectCount,
        long reviewCount,
        long aiCallCount,
        long aiFailureCount,
        long? inputTokens,
        long? outputTokens,
        DateTimeOffset updatedAt)
    {
        RequestCount = requestCount;
        IdempotencyReplayCount = idempotencyReplayCount;
        ItemCount = itemCount;
        PassCount = passCount;
        RejectCount = rejectCount;
        ReviewCount = reviewCount;
        AiCallCount = aiCallCount;
        AiFailureCount = aiFailureCount;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        UpdatedAt = updatedAt;
    }
}

/// <summary>按应用、API Key 和自然日保存的可重建用量投影。</summary>
public sealed class UsageDaily
{
    private UsageDaily()
    {
    }

    public UsageDaily(
        Guid tenantId,
        Guid applicationId,
        Guid apiKeyId,
        DateTimeOffset bucketStart)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        ApiKeyId = apiKeyId;
        BucketStart = bucketStart;
        CreatedAt = bucketStart;
        UpdatedAt = bucketStart;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid ApiKeyId { get; private set; }

    public DateTimeOffset BucketStart { get; private set; }

    public long RequestCount { get; private set; }

    public long IdempotencyReplayCount { get; private set; }

    public long ItemCount { get; private set; }

    public long PassCount { get; private set; }

    public long RejectCount { get; private set; }

    public long ReviewCount { get; private set; }

    public long AiCallCount { get; private set; }

    public long AiFailureCount { get; private set; }

    public long? InputTokens { get; private set; }

    public long? OutputTokens { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public void Replace(
        long requestCount,
        long idempotencyReplayCount,
        long itemCount,
        long passCount,
        long rejectCount,
        long reviewCount,
        long aiCallCount,
        long aiFailureCount,
        long? inputTokens,
        long? outputTokens,
        DateTimeOffset updatedAt)
    {
        RequestCount = requestCount;
        IdempotencyReplayCount = idempotencyReplayCount;
        ItemCount = itemCount;
        PassCount = passCount;
        RejectCount = rejectCount;
        ReviewCount = reviewCount;
        AiCallCount = aiCallCount;
        AiFailureCount = aiFailureCount;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        UpdatedAt = updatedAt;
    }
}

/// <summary>记录聚合投影已消费的 Outbox 事件，避免重复计量。</summary>
public sealed class UsageConsumedEvent
{
    private UsageConsumedEvent()
    {
    }

    public UsageConsumedEvent(string consumerName, Guid outboxEventId, DateTimeOffset consumedAt)
    {
        Id = Guid.CreateVersion7();
        ConsumerName = consumerName;
        OutboxEventId = outboxEventId;
        ConsumedAt = consumedAt;
    }

    public Guid Id { get; private set; }

    public string ConsumerName { get; private set; } = string.Empty;

    public Guid OutboxEventId { get; private set; }

    public DateTimeOffset ConsumedAt { get; private set; }
}
