namespace VeriScan.Domain.Entities;

/// <summary>应用当前生效的 Webhook 目标与验证状态。</summary>
public sealed class ApplicationWebhook
{
    private ApplicationWebhook()
    {
    }

    public ApplicationWebhook(
        Guid tenantId,
        Guid applicationId,
        string endpointUrl,
        string providerApplicationId,
        string providerEndpointId,
        DateTimeOffset createdAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        EndpointUrl = endpointUrl;
        ProviderApplicationId = providerApplicationId;
        ProviderEndpointId = providerEndpointId;
        Revision = 1;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public string EndpointUrl { get; private set; } = string.Empty;

    public string ProviderApplicationId { get; private set; } = string.Empty;

    public string ProviderEndpointId { get; private set; } = string.Empty;

    public int Revision { get; private set; }

    public bool IsEnabled { get; private set; }

    public Guid? LastTestId { get; private set; }

    public int? LastTestRevision { get; private set; }

    public WebhookTestOutcome? LastTestOutcome { get; private set; }

    public int? LastTestHttpStatusCode { get; private set; }

    public long? LastTestLatencyMilliseconds { get; private set; }

    public DateTimeOffset? LastTestedAt { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public bool HasSuccessfulCurrentTest =>
        LastTestRevision == Revision && LastTestOutcome == WebhookTestOutcome.Succeeded;

    public void PrepareEndpointChange(DateTimeOffset updatedAt)
    {
        Revision++;
        ResetVerification();
        UpdatedAt = updatedAt;
    }

    public void UpdateEndpoint(
        string endpointUrl,
        string providerApplicationId,
        string providerEndpointId,
        DateTimeOffset updatedAt)
    {
        ProviderApplicationId = providerApplicationId;
        ProviderEndpointId = providerEndpointId;
        if (!string.Equals(EndpointUrl, endpointUrl, StringComparison.Ordinal))
        {
            EndpointUrl = endpointUrl;
        }

        UpdatedAt = updatedAt;
    }

    public void SetEnabled(bool enabled, DateTimeOffset updatedAt)
    {
        if (enabled && !HasSuccessfulCurrentTest)
        {
            throw new InvalidOperationException("当前 Webhook 地址必须先通过连接测试。");
        }

        IsEnabled = enabled;
        UpdatedAt = updatedAt;
    }

    public void PrepareSecretRotation(DateTimeOffset rotatedAt)
    {
        Revision++;
        ResetVerification();
        UpdatedAt = rotatedAt;
    }

    public void CompleteSecretRotation(DateTimeOffset rotatedAt)
    {
        UpdatedAt = rotatedAt;
    }

    public void RecordTestResult(
        Guid testId,
        int revision,
        WebhookTestOutcome outcome,
        int? httpStatusCode,
        long? latencyMilliseconds,
        DateTimeOffset testedAt)
    {
        if (revision != Revision)
        {
            return;
        }

        LastTestId = testId;
        LastTestRevision = revision;
        LastTestOutcome = outcome;
        LastTestHttpStatusCode = httpStatusCode;
        LastTestLatencyMilliseconds = latencyMilliseconds;
        LastTestedAt = testedAt;
        if (outcome == WebhookTestOutcome.Failed)
        {
            IsEnabled = false;
        }
        UpdatedAt = testedAt;
    }

    public void RecordTestRequested(Guid testId, DateTimeOffset requestedAt)
    {
        LastTestId = testId;
        LastTestRevision = Revision;
        LastTestOutcome = null;
        LastTestHttpStatusCode = null;
        LastTestLatencyMilliseconds = null;
        LastTestedAt = null;
        UpdatedAt = requestedAt;
    }

    private void ResetVerification()
    {
        IsEnabled = false;
        LastTestId = null;
        LastTestRevision = null;
        LastTestOutcome = null;
        LastTestHttpStatusCode = null;
        LastTestLatencyMilliseconds = null;
        LastTestedAt = null;
    }
}

public enum WebhookTestOutcome
{
    Succeeded,
    Failed
}

/// <summary>将业务终态或连接测试可靠提交给 Webhook 投递服务。</summary>
public sealed class WebhookPublication
{
    private WebhookPublication()
    {
    }

    public WebhookPublication(
        Guid id,
        Guid tenantId,
        Guid applicationId,
        Guid applicationWebhookId,
        int configurationRevision,
        string providerApplicationId,
        string providerEndpointId,
        WebhookPublicationKind kind,
        string eventType,
        string payloadJson,
        string deduplicationKey,
        DateTimeOffset createdAt)
    {
        Id = id;
        TenantId = tenantId;
        ApplicationId = applicationId;
        ApplicationWebhookId = applicationWebhookId;
        ConfigurationRevision = configurationRevision;
        ProviderApplicationId = providerApplicationId;
        ProviderEndpointId = providerEndpointId;
        Kind = kind;
        EventType = eventType;
        PayloadJson = payloadJson;
        DeduplicationKey = deduplicationKey;
        Status = WebhookPublicationStatus.Queued;
        AvailableAt = createdAt;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
    }

    public Guid Id { get; private set; }

    public Guid TenantId { get; private set; }

    public Guid ApplicationId { get; private set; }

    public Guid ApplicationWebhookId { get; private set; }

    public int ConfigurationRevision { get; private set; }

    public string ProviderApplicationId { get; private set; } = string.Empty;

    public string ProviderEndpointId { get; private set; } = string.Empty;

    public WebhookPublicationKind Kind { get; private set; }

    public string EventType { get; private set; } = string.Empty;

    /// <summary>负载不得包含审核原文、凭证或完整响应。</summary>
    public string PayloadJson { get; private set; } = string.Empty;

    public string DeduplicationKey { get; private set; } = string.Empty;

    public WebhookPublicationStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public int TestPollCount { get; private set; }

    public string? LeaseOwner { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public DateTimeOffset AvailableAt { get; private set; }

    public string? ProviderMessageId { get; private set; }

    public string? LastErrorCode { get; private set; }

    public int? ResponseStatusCode { get; private set; }

    public long? ResponseLatencyMilliseconds { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public DateTimeOffset? CompletedAt { get; private set; }

    public bool CanBeClaimed(DateTimeOffset now) =>
        (Status is WebhookPublicationStatus.Queued or WebhookPublicationStatus.Delivering) &&
        AvailableAt <= now &&
        (LeaseExpiresAt is null || LeaseExpiresAt <= now);

    public void Claim(string leaseOwner, DateTimeOffset now, TimeSpan leaseDuration)
    {
        if (!CanBeClaimed(now))
        {
            throw new InvalidOperationException("Webhook 事件当前不可领取。");
        }

        LeaseOwner = leaseOwner;
        LeaseExpiresAt = now.Add(leaseDuration);
        UpdatedAt = now;
    }

    public void RecordSubmissionAttempt(DateTimeOffset attemptedAt)
    {
        AttemptCount++;
        UpdatedAt = attemptedAt;
    }

    public void MarkProviderAccepted(string providerMessageId, DateTimeOffset acceptedAt)
    {
        ProviderMessageId = providerMessageId;
        Status = Kind == WebhookPublicationKind.Test
            ? WebhookPublicationStatus.Delivering
            : WebhookPublicationStatus.ProviderAccepted;
        AvailableAt = acceptedAt;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        LastErrorCode = null;
        UpdatedAt = acceptedAt;
        if (Kind == WebhookPublicationKind.Notification)
        {
            CompletedAt = acceptedAt;
        }
    }

    public void ScheduleTestPoll(DateTimeOffset now, TimeSpan delay)
    {
        TestPollCount++;
        Status = WebhookPublicationStatus.Delivering;
        AvailableAt = now.Add(delay);
        LeaseOwner = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
    }

    public void MarkTestSucceeded(int? statusCode, long? latencyMilliseconds, DateTimeOffset completedAt)
    {
        Status = WebhookPublicationStatus.Succeeded;
        ResponseStatusCode = statusCode;
        ResponseLatencyMilliseconds = latencyMilliseconds;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        LastErrorCode = null;
        UpdatedAt = completedAt;
        CompletedAt = completedAt;
    }

    public void MarkTestFailed(
        string errorCode,
        int? statusCode,
        long? latencyMilliseconds,
        DateTimeOffset completedAt)
    {
        Status = WebhookPublicationStatus.Failed;
        LastErrorCode = errorCode;
        ResponseStatusCode = statusCode;
        ResponseLatencyMilliseconds = latencyMilliseconds;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        UpdatedAt = completedAt;
        CompletedAt = completedAt;
    }

    public void RetryOrDeadLetter(
        string errorCode,
        DateTimeOffset now,
        TimeSpan retryDelay,
        int maximumAttempts)
    {
        LastErrorCode = errorCode;
        LeaseOwner = null;
        LeaseExpiresAt = null;
        UpdatedAt = now;
        if (AttemptCount >= maximumAttempts)
        {
            Status = WebhookPublicationStatus.DeadLetter;
            CompletedAt = now;
            return;
        }

        Status = WebhookPublicationStatus.Queued;
        AvailableAt = now.Add(retryDelay);
    }
}

public enum WebhookPublicationKind
{
    Notification,
    Test
}

public enum WebhookPublicationStatus
{
    Queued,
    Delivering,
    ProviderAccepted,
    Succeeded,
    Failed,
    DeadLetter
}
