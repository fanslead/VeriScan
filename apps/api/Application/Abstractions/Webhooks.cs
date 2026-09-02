namespace VeriScan.Application.Abstractions;

/// <summary>Webhook 供应商不可用时返回的安全异常。</summary>
public sealed class WebhookProviderUnavailableException()
    : ApplicationBaseException("webhook_provider_unavailable", "Webhook 投递服务暂不可用。");

/// <summary>Webhook 供应商返回非预期错误时使用的安全异常。</summary>
public sealed class WebhookProviderRequestException(string errorCode, string message)
    : ApplicationBaseException(errorCode, message);

/// <summary>应用在 Webhook 供应商中的应用和地址版本端点标识。</summary>
public sealed record WebhookEndpointRegistration(
    string ProviderApplicationId,
    string ProviderEndpointId,
    string? SigningSecret);

/// <summary>Webhook 供应商接受消息后的回执。</summary>
public sealed record WebhookPublishReceipt(string ProviderMessageId);

/// <summary>供应商端点最近一次投递尝试的状态。</summary>
public enum WebhookAttemptState
{
    Pending,
    Succeeded,
    Failed
}

/// <summary>Webhook 供应商投递尝试查询结果。</summary>
public sealed record WebhookAttemptResult(
    WebhookAttemptState State,
    int? HttpStatusCode,
    long? LatencyMilliseconds,
    string? FailureCode);

/// <summary>Webhook 供应商适配器边界。</summary>
public interface IWebhookProvider
{
    /// <summary>创建或更新应用及其按地址隔离的版本端点。</summary>
    Task<WebhookEndpointRegistration> ConfigureEndpointAsync(
        Guid applicationId,
        string applicationName,
        string endpointUrl,
        string? currentProviderEndpointId,
        bool revealSecret,
        CancellationToken cancellationToken);

    /// <summary>轮换端点签名密钥并返回新密钥。</summary>
    Task<string> RotateSecretAsync(
        string providerApplicationId,
        string providerEndpointId,
        CancellationToken cancellationToken);

    /// <summary>以事件 ID 作为供应商幂等键发布消息。</summary>
    Task<WebhookPublishReceipt> PublishAsync(
        string providerApplicationId,
        string providerEndpointId,
        Guid eventId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken);

    /// <summary>查询应用、消息和端点对应的最近一次投递尝试。</summary>
    Task<WebhookAttemptResult> GetAttemptAsync(
        string providerApplicationId,
        string providerMessageId,
        string providerEndpointId,
        CancellationToken cancellationToken);
}
