namespace VeriScan.Domain.Entities;

/// <summary>记录一次审核项的 AI 调用结果和供应商计量事实。</summary>
public sealed class AiInvocation
{
    private AiInvocation()
    {
    }

    public AiInvocation(
        Guid tenantId,
        Guid applicationId,
        Guid apiKeyId,
        Guid moderationRequestId,
        Guid moderationItemId,
        string outcome,
        string? configurationRevision,
        string? providerRequestId,
        int attemptNumber,
        int? inputTokens,
        int? outputTokens,
        string? failureCode,
        long? latencyMilliseconds,
        DateTimeOffset startedAt,
        DateTimeOffset completedAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        ApiKeyId = apiKeyId;
        ModerationRequestId = moderationRequestId;
        ModerationItemId = moderationItemId;
        Outcome = outcome;
        ConfigurationRevision = configurationRevision;
        ProviderRequestId = providerRequestId;
        AttemptNumber = attemptNumber;
        InputTokens = inputTokens;
        OutputTokens = outputTokens;
        FailureCode = failureCode;
        LatencyMilliseconds = latencyMilliseconds;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        CreatedAt = completedAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid ApiKeyId { get; private set; }

    public Guid ModerationRequestId { get; private set; }

    public Guid ModerationItemId { get; private set; }

    public string Outcome { get; private set; } = string.Empty;

    public string? ConfigurationRevision { get; private set; }

    public string? ProviderRequestId { get; private set; }

    public int AttemptNumber { get; private set; }

    public int? InputTokens { get; private set; }

    public int? OutputTokens { get; private set; }

    public string? FailureCode { get; private set; }

    public long? LatencyMilliseconds { get; private set; }

    public DateTimeOffset StartedAt { get; private set; }

    public DateTimeOffset CompletedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }
}
