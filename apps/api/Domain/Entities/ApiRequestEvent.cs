namespace VeriScan.Domain.Entities;

/// <summary>记录一次已通过 API 层认证或业务处理的请求事实。</summary>
public sealed class ApiRequestEvent
{
    private ApiRequestEvent()
    {
    }

    public ApiRequestEvent(
        Guid? tenantId,
        Guid? applicationId,
        Guid? apiKeyId,
        Guid? moderationRequestId,
        string routeTemplate,
        string authenticationOutcome,
        string idempotencyOutcome,
        int httpStatusCode,
        int? itemCount,
        long? latencyMilliseconds,
        DateTimeOffset occurredAt)
    {
        Id = Guid.CreateVersion7();
        TenantId = tenantId;
        ApplicationId = applicationId;
        ApiKeyId = apiKeyId;
        ModerationRequestId = moderationRequestId;
        RouteTemplate = routeTemplate;
        AuthenticationOutcome = authenticationOutcome;
        IdempotencyOutcome = idempotencyOutcome;
        HttpStatusCode = httpStatusCode;
        ItemCount = itemCount;
        LatencyMilliseconds = latencyMilliseconds;
        OccurredAt = occurredAt;
    }

    public Guid Id { get; private set; }

    public Guid? TenantId { get; private set; }

    public Guid? ApplicationId { get; private set; }

    public Guid? ApiKeyId { get; private set; }

    public Guid? ModerationRequestId { get; private set; }

    public string RouteTemplate { get; private set; } = string.Empty;

    public string AuthenticationOutcome { get; private set; } = string.Empty;

    public string IdempotencyOutcome { get; private set; } = string.Empty;

    public int HttpStatusCode { get; private set; }

    public int? ItemCount { get; private set; }

    public long? LatencyMilliseconds { get; private set; }

    public DateTimeOffset OccurredAt { get; private set; }
}
