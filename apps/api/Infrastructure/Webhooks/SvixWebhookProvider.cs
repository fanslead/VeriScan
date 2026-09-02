using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Newtonsoft.Json.Linq;
using Svix;
using Svix.Models;
using VeriScan.Application.Abstractions;

namespace VeriScan.Infrastructure.Webhooks;

/// <summary>基于官方 Svix .NET SDK 的 Webhook 供应商适配器。</summary>
public sealed partial class SvixWebhookProvider(
    IOptionsMonitor<WebhookProviderOptions> options,
    ILogger<SvixWebhookProvider> logger) : IWebhookProvider
{
    private readonly object clientLock = new();
    private SvixClient? client;
    private string? clientServerUrl;
    private string? clientAuthToken;
    private int clientTimeoutMilliseconds;

    public async Task<WebhookEndpointRegistration> ConfigureEndpointAsync(
        Guid applicationId,
        string applicationName,
        string endpointUrl,
        string? currentProviderEndpointId,
        bool revealSecret,
        CancellationToken cancellationToken)
    {
        var currentOptions = GetEnabledOptions();
        ValidateApplicationInput(applicationId, applicationName, endpointUrl);

        var application = await ExecuteAsync(
            "application_configure",
            currentOptions,
            (svix, token) => svix.Application.GetOrCreateAsync(
                new ApplicationIn
                {
                    Name = applicationName,
                    Uid = BuildApplicationUid(applicationId)
                },
                cancellationToken: token),
            cancellationToken);

        var endpointUid = BuildEndpointUid(endpointUrl);
        var hasCurrentEndpoint = !string.IsNullOrWhiteSpace(currentProviderEndpointId);
        if (hasCurrentEndpoint)
        {
            ValidateProviderIdentifiers(application.Id, currentProviderEndpointId!);
        }

        // 新地址先以禁用状态创建；相同地址则保留现有 Channel，避免幂等保存形成投递空窗。
        var endpoint = await ExecuteAsync(
            "endpoint_configure",
            currentOptions,
            (svix, token) => svix.Endpoint.UpsertAsync(
                application.Id,
                endpointUid,
                new EndpointUpsertIn
                {
                    Url = endpointUrl,
                    Uid = endpointUid,
                    Disabled = !hasCurrentEndpoint,
                    Channels = [hasCurrentEndpoint ? currentProviderEndpointId! : endpointUid]
                },
                token),
            cancellationToken);

        var providerEndpointRecreated = hasCurrentEndpoint &&
            !string.Equals(endpoint.Id, currentProviderEndpointId, StringComparison.Ordinal);
        if (!hasCurrentEndpoint || providerEndpointRecreated)
        {
            endpoint = await ExecuteAsync(
                "endpoint_activate",
                currentOptions,
                (svix, token) => svix.Endpoint.UpsertAsync(
                    application.Id,
                    endpointUid,
                    new EndpointUpsertIn
                    {
                        Url = endpointUrl,
                        Uid = endpointUid,
                        Disabled = false,
                        Channels = [endpoint.Id]
                    },
                    token),
                cancellationToken);
        }

        string? signingSecret = null;
        if (revealSecret || providerEndpointRecreated)
        {
            var secret = await ExecuteAsync(
                "endpoint_secret_read",
                currentOptions,
                (svix, token) => svix.Endpoint.GetSecretAsync(
                    application.Id,
                    endpoint.Id,
                    token),
                cancellationToken);
            signingSecret = string.IsNullOrWhiteSpace(secret.Key)
                ? throw new WebhookProviderRequestException(
                    "webhook_provider_secret_missing",
                    "Webhook 签名密钥暂不可用。")
                : secret.Key;
        }

        return new WebhookEndpointRegistration(application.Id, endpoint.Id, signingSecret);
    }

    public async Task<string> RotateSecretAsync(
        string providerApplicationId,
        string providerEndpointId,
        CancellationToken cancellationToken)
    {
        var currentOptions = GetEnabledOptions();
        ValidateProviderIdentifiers(providerApplicationId, providerEndpointId);

        var rotated = await ExecuteAsync(
            "endpoint_secret_rotate",
            currentOptions,
            (svix, token) => svix.Endpoint.RotateSecretAsync(
                providerApplicationId,
                providerEndpointId,
                new EndpointSecretRotateIn
                {
                    GracePeriodSeconds = checked((uint)currentOptions.SecretRotationGraceSeconds)
                },
                cancellationToken: token),
            cancellationToken);

        if (!rotated)
        {
            throw new WebhookProviderRequestException(
                "webhook_provider_secret_rotate_failed",
                "Webhook 签名密钥轮换失败。");
        }

        var secret = await ExecuteAsync(
            "endpoint_secret_read",
            currentOptions,
            (svix, token) => svix.Endpoint.GetSecretAsync(
                providerApplicationId,
                providerEndpointId,
                token),
            cancellationToken);

        return string.IsNullOrWhiteSpace(secret.Key)
            ? throw new WebhookProviderRequestException(
                "webhook_provider_secret_missing",
                "Webhook 签名密钥暂不可用。")
            : secret.Key;
    }

    public async Task<WebhookPublishReceipt> PublishAsync(
        string providerApplicationId,
        string providerEndpointId,
        Guid eventId,
        string eventType,
        string payloadJson,
        CancellationToken cancellationToken)
    {
        var currentOptions = GetEnabledOptions();
        ValidateProviderIdentifiers(providerApplicationId, providerEndpointId);
        if (eventId == Guid.Empty)
        {
            throw new RequestValidationException("Webhook 事件标识不能为空。");
        }

        if (string.IsNullOrWhiteSpace(eventType))
        {
            throw new RequestValidationException("Webhook 事件类型不能为空。");
        }

        JToken payload;
        try
        {
            payload = JToken.Parse(payloadJson);
        }
        catch (Newtonsoft.Json.JsonException)
        {
            throw new RequestValidationException("Webhook 事件负载格式无效。");
        }

        var eventIdText = eventId.ToString("D");
        var message = await ExecuteAsync(
            "message_publish",
            currentOptions,
            (svix, token) => svix.Message.CreateAsync(
                providerApplicationId,
                new MessageIn
                {
                    EventId = eventIdText,
                    EventType = eventType,
                    Payload = payload,
                    Channels = [providerEndpointId]
                },
                new MessageCreateOptions
                {
                    WithContent = false,
                    IdempotencyKey = eventIdText
                },
                token),
            cancellationToken);

        if (string.IsNullOrWhiteSpace(message.Id))
        {
            throw new WebhookProviderRequestException(
                "webhook_provider_message_id_missing",
                "Webhook 投递服务未返回消息标识。");
        }

        return new WebhookPublishReceipt(message.Id);
    }

    public async Task<WebhookAttemptResult> GetAttemptAsync(
        string providerApplicationId,
        string providerMessageId,
        string providerEndpointId,
        CancellationToken cancellationToken)
    {
        var currentOptions = GetEnabledOptions();
        ValidateProviderIdentifiers(providerApplicationId, providerEndpointId);
        if (string.IsNullOrWhiteSpace(providerMessageId))
        {
            throw new RequestValidationException("Webhook 消息标识不能为空。");
        }

        var attempts = await ExecuteAsync(
            "message_attempt_query",
            currentOptions,
            (svix, token) => svix.MessageAttempt.ListByMsgAsync(
                providerApplicationId,
                providerMessageId,
                new MessageAttemptListByMsgOptions
                {
                    EndpointId = providerEndpointId,
                    Limit = 1,
                    WithContent = false,
                    ExpandedStatuses = true
                },
                token),
            cancellationToken);

        var attempt = attempts.Data
            .OrderByDescending(item => item.Timestamp)
            .FirstOrDefault();
        if (attempt is null)
        {
            return new WebhookAttemptResult(
                WebhookAttemptState.Pending,
                null,
                null,
                null);
        }

        var state = attempt.Status switch
        {
            MessageStatus.Success => WebhookAttemptState.Succeeded,
            MessageStatus.Fail or MessageStatus.Canceled => WebhookAttemptState.Failed,
            _ => WebhookAttemptState.Pending
        };
        int? statusCode = attempt.ResponseStatusCode > 0
            ? attempt.ResponseStatusCode
            : null;
        long? latency = state != WebhookAttemptState.Pending && attempt.ResponseDurationMs >= 0
            ? attempt.ResponseDurationMs
            : null;
        var failureCode = state == WebhookAttemptState.Failed
            ? attempt.Status switch
            {
                MessageStatus.Canceled => "provider_delivery_canceled",
                _ => "provider_delivery_failed"
            }
            : null;

        return new WebhookAttemptResult(state, statusCode, latency, failureCode);
    }

    private WebhookProviderOptions GetEnabledOptions()
    {
        var currentOptions = options.CurrentValue;
        if (!currentOptions.Enabled)
        {
            throw new WebhookProviderUnavailableException();
        }

        if (string.IsNullOrWhiteSpace(currentOptions.AuthToken) ||
            !WebhookProviderOptions.TryCreateServerUri(currentOptions.ServerUrl, out _))
        {
            throw new WebhookProviderUnavailableException();
        }

        return currentOptions;
    }

    private SvixClient GetClient(WebhookProviderOptions currentOptions)
    {
        var serverUrl = currentOptions.ServerUrl.TrimEnd('/');
        lock (clientLock)
        {
            if (client is null ||
                !string.Equals(clientServerUrl, serverUrl, StringComparison.Ordinal) ||
                !string.Equals(clientAuthToken, currentOptions.AuthToken, StringComparison.Ordinal) ||
                clientTimeoutMilliseconds != currentOptions.TimeoutMilliseconds)
            {
                client = new SvixClient(
                    currentOptions.AuthToken,
                    new SvixOptions(
                        serverUrl: serverUrl,
                        timeoutMilliseconds: currentOptions.TimeoutMilliseconds));
                clientServerUrl = serverUrl;
                clientAuthToken = currentOptions.AuthToken;
                clientTimeoutMilliseconds = currentOptions.TimeoutMilliseconds;
            }

            return client;
        }
    }

    private async Task<T> ExecuteAsync<T>(
        string operation,
        WebhookProviderOptions currentOptions,
        Func<SvixClient, CancellationToken, Task<T>> action,
        CancellationToken cancellationToken)
    {
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(currentOptions.TimeoutMilliseconds);

        try
        {
            return await action(GetClient(currentOptions), timeoutSource.Token)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            LogProviderTimeout(operation, currentOptions.TimeoutMilliseconds);
            throw new RequestTimeoutException("Webhook 投递服务响应超时。");
        }
        catch (ApiException exception)
        {
            throw MapApiException(operation, exception.ErrorCode);
        }
        catch (HttpRequestException)
        {
            LogProviderNetworkFailure(operation);
            throw new WebhookProviderUnavailableException();
        }
    }

    private Exception MapApiException(string operation, int statusCode)
    {
        LogProviderStatusFailure(operation, statusCode);

        return statusCode switch
        {
            408 or 504 => new RequestTimeoutException("Webhook 投递服务响应超时。"),
            409 => new RequestConflictException("Webhook 配置或消息已存在。"),
            429 or >= 500 => new WebhookProviderUnavailableException(),
            _ => new WebhookProviderRequestException(
                "webhook_provider_error",
                "Webhook 投递服务请求失败。")
        };
    }

    private static string BuildApplicationUid(Guid applicationId) =>
        $"veriscan-{applicationId:N}";

    private static string BuildEndpointUid(string endpointUrl)
    {
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(endpointUrl));
        return $"veriscan-{Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant()}";
    }

    private static void ValidateApplicationInput(
        Guid applicationId,
        string applicationName,
        string endpointUrl)
    {
        if (applicationId == Guid.Empty || string.IsNullOrWhiteSpace(applicationName))
        {
            throw new RequestValidationException("Webhook 应用信息不能为空。");
        }

        if (!Uri.TryCreate(endpointUrl, UriKind.Absolute, out var endpointUri) ||
            endpointUri.Scheme is not ("http" or "https") ||
            !string.IsNullOrEmpty(endpointUri.UserInfo) ||
            !string.IsNullOrEmpty(endpointUri.Fragment))
        {
            throw new RequestValidationException("Webhook 地址格式无效。");
        }
    }

    private static void ValidateProviderIdentifiers(
        string providerApplicationId,
        string providerEndpointId)
    {
        if (string.IsNullOrWhiteSpace(providerApplicationId) ||
            string.IsNullOrWhiteSpace(providerEndpointId))
        {
            throw new RequestValidationException("Webhook 供应商标识不能为空。");
        }
    }

    [LoggerMessage(
        EventId = 42_001,
        Level = LogLevel.Warning,
        Message = "Webhook 供应商操作超时，操作={Operation}，超时毫秒={TimeoutMilliseconds}")]
    private partial void LogProviderTimeout(string operation, int timeoutMilliseconds);

    [LoggerMessage(
        EventId = 42_002,
        Level = LogLevel.Warning,
        Message = "Webhook 供应商网络请求失败，操作={Operation}")]
    private partial void LogProviderNetworkFailure(string operation);

    [LoggerMessage(
        EventId = 42_003,
        Level = LogLevel.Warning,
        Message = "Webhook 供应商返回错误，操作={Operation}，状态码={StatusCode}")]
    private partial void LogProviderStatusFailure(string operation, int statusCode);
}
