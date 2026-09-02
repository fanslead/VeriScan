namespace VeriScan.Application.Contracts;

/// <summary>保存应用 Webhook 地址。</summary>
public sealed record SaveApplicationWebhookRequest
{
    public required string EndpointUrl { get; init; }
}

/// <summary>切换应用 Webhook 通知状态。</summary>
public sealed record SetApplicationWebhookStatusRequest
{
    public required bool Enabled { get; init; }
}

/// <summary>应用 Webhook 当前配置，不返回供应商内部标识或签名密钥。</summary>
public sealed record ApplicationWebhookResponse(
    bool Configured,
    Guid? Id,
    Guid ApplicationId,
    string? EndpointUrl,
    bool Enabled,
    int? Revision,
    bool CurrentRevisionTested,
    Guid? LastTestId,
    WebhookTestStatus? LastTestStatus,
    int? LastTestHttpStatusCode,
    long? LastTestLatencyMilliseconds,
    DateTimeOffset? LastTestedAt,
    DateTimeOffset? UpdatedAt);

/// <summary>保存地址时可能返回的一次性签名密钥。</summary>
public sealed record ApplicationWebhookSavedResponse(
    ApplicationWebhookResponse Webhook,
    string? SigningSecret);

/// <summary>轮换后仅返回一次的新签名密钥。</summary>
public sealed record ApplicationWebhookSecretResponse(
    string SigningSecret,
    DateTimeOffset RotatedAt);

/// <summary>连接测试已进入可靠投递队列。</summary>
public sealed record ApplicationWebhookTestAcceptedResponse(
    Guid TestId,
    string StatusUrl,
    DateTimeOffset SubmittedAt);

/// <summary>连接测试的当前结果。</summary>
public sealed record ApplicationWebhookTestResponse(
    Guid TestId,
    Guid ApplicationId,
    int ConfigurationRevision,
    WebhookTestStatus Status,
    int? HttpStatusCode,
    long? LatencyMilliseconds,
    string? FailureCode,
    DateTimeOffset SubmittedAt,
    DateTimeOffset? CompletedAt);

public enum WebhookTestStatus
{
    Pending,
    Delivering,
    Succeeded,
    Failed
}
