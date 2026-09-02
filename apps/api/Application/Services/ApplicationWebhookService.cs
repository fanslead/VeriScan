using System.Text.Json;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IApplicationWebhookService
{
    Task<ApplicationWebhookResponse> GetAsync(
        Guid applicationId,
        CancellationToken cancellationToken);

    Task<ApplicationWebhookSavedResponse> SaveAsync(
        Guid applicationId,
        SaveApplicationWebhookRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationWebhookResponse> SetStatusAsync(
        Guid applicationId,
        SetApplicationWebhookStatusRequest request,
        CancellationToken cancellationToken);

    Task<ApplicationWebhookTestAcceptedResponse> TestAsync(
        Guid applicationId,
        CancellationToken cancellationToken);

    Task<ApplicationWebhookTestResponse> GetTestAsync(
        Guid applicationId,
        Guid testId,
        CancellationToken cancellationToken);

    Task<ApplicationWebhookSecretResponse> RotateSecretAsync(
        Guid applicationId,
        CancellationToken cancellationToken);
}

public sealed class ApplicationWebhookService(
    IApplicationWebhookStore store,
    IWebhookProvider provider,
    IOperationalFactService operationalFactService) : IApplicationWebhookService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task<ApplicationWebhookResponse> GetAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        _ = await GetApplicationAsync(applicationId, cancellationToken);
        var webhook = await store.GetByApplicationAsync(applicationId, cancellationToken);
        return ToResponse(applicationId, webhook);
    }

    public async Task<ApplicationWebhookSavedResponse> SaveAsync(
        Guid applicationId,
        SaveApplicationWebhookRequest request,
        CancellationToken cancellationToken)
    {
        var application = await GetApplicationAsync(applicationId, cancellationToken);
        var endpointUrl = ValidateEndpointUrl(request.EndpointUrl);
        var webhook = await store.GetByApplicationAsync(applicationId, cancellationToken);
        var endpointChanged = webhook is null ||
            !string.Equals(webhook.EndpointUrl, endpointUrl, StringComparison.Ordinal);
        if (webhook is not null && endpointChanged)
        {
            var preparingAt = DateTimeOffset.UtcNow;
            var beforePreparingJson = SafeAuditPayload(webhook);
            webhook.PrepareEndpointChange(preparingAt);
            await RecordChangeAsync(
                webhook,
                "application.webhook_change_started",
                beforePreparingJson,
                preparingAt,
                cancellationToken);
            await SaveChangesAsync(cancellationToken);
        }

        var registration = await provider.ConfigureEndpointAsync(
            application.Id,
            application.Name,
            endpointUrl,
            endpointChanged ? null : webhook!.ProviderEndpointId,
            endpointChanged,
            cancellationToken);
        var changedAt = DateTimeOffset.UtcNow;
        var beforeJson = SafeAuditPayload(webhook);
        var providerEndpointRecreated = webhook is not null &&
            !endpointChanged &&
            !string.Equals(
                webhook.ProviderEndpointId,
                registration.ProviderEndpointId,
                StringComparison.Ordinal);
        if (webhook is null)
        {
            webhook = new ApplicationWebhook(
                application.TenantId,
                application.Id,
                endpointUrl,
                registration.ProviderApplicationId,
                registration.ProviderEndpointId,
                changedAt);
            await store.AddAsync(webhook, cancellationToken);
        }
        else
        {
            if (providerEndpointRecreated)
            {
                webhook.PrepareEndpointChange(changedAt);
            }

            webhook.UpdateEndpoint(
                endpointUrl,
                registration.ProviderApplicationId,
                registration.ProviderEndpointId,
                changedAt);
        }

        await RecordChangeAsync(
            webhook,
            "application.webhook_configured",
            beforeJson,
            changedAt,
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return new ApplicationWebhookSavedResponse(
            ToResponse(applicationId, webhook),
            endpointChanged || providerEndpointRecreated
                ? registration.SigningSecret
                : null);
    }

    public async Task<ApplicationWebhookResponse> SetStatusAsync(
        Guid applicationId,
        SetApplicationWebhookStatusRequest request,
        CancellationToken cancellationToken)
    {
        _ = await GetApplicationAsync(applicationId, cancellationToken);
        var webhook = await GetWebhookAsync(applicationId, cancellationToken);
        if (webhook.IsEnabled == request.Enabled)
        {
            return ToResponse(applicationId, webhook);
        }

        var changedAt = DateTimeOffset.UtcNow;
        var beforeJson = SafeAuditPayload(webhook);
        try
        {
            webhook.SetEnabled(request.Enabled, changedAt);
        }
        catch (InvalidOperationException)
        {
            throw new RequestConflictException("请先让当前 Webhook 地址通过连接测试，再启用通知。");
        }

        await RecordChangeAsync(
            webhook,
            request.Enabled
                ? "application.webhook_enabled"
                : "application.webhook_disabled",
            beforeJson,
            changedAt,
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return ToResponse(applicationId, webhook);
    }

    public async Task<ApplicationWebhookTestAcceptedResponse> TestAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var application = await GetApplicationAsync(applicationId, cancellationToken);
        var webhook = await GetWebhookAsync(applicationId, cancellationToken);
        var submittedAt = DateTimeOffset.UtcNow;
        var testId = Guid.CreateVersion7();
        var payload = JsonSerializer.Serialize(
            new
            {
                schemaVersion = "1.0",
                eventId = testId,
                eventType = "webhook.test",
                occurredAt = submittedAt,
                data = new
                {
                    applicationId = application.Id,
                    message = "VeriScan Webhook 连接测试"
                }
            },
            JsonOptions);
        var publication = new WebhookPublication(
            testId,
            webhook.TenantId,
            webhook.ApplicationId,
            webhook.Id,
            webhook.Revision,
            webhook.ProviderApplicationId,
            webhook.ProviderEndpointId,
            WebhookPublicationKind.Test,
            "webhook.test",
            payload,
            $"webhook-test:{testId:N}",
            submittedAt);
        webhook.RecordTestRequested(testId, submittedAt);
        await store.AddPublicationAsync(publication, cancellationToken);
        await operationalFactService.RecordAuditAsync(
            new AuditEntry(
                webhook.TenantId,
                webhook.ApplicationId,
                null,
                "admin",
                null,
                "application.webhook_test_requested",
                "application_webhook",
                webhook.Id.ToString(),
                null,
                SafeAuditPayload(webhook),
                null,
                submittedAt),
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return new ApplicationWebhookTestAcceptedResponse(
            testId,
            $"/api/admin/v1/applications/{applicationId}/webhook/tests/{testId}",
            submittedAt);
    }

    public async Task<ApplicationWebhookTestResponse> GetTestAsync(
        Guid applicationId,
        Guid testId,
        CancellationToken cancellationToken)
    {
        _ = await GetApplicationAsync(applicationId, cancellationToken);
        var publication = await store.GetTestAsync(applicationId, testId, cancellationToken)
            ?? throw new ResourceNotFoundException("Webhook 连接测试不存在。");
        return ToTestResponse(publication);
    }

    public async Task<ApplicationWebhookSecretResponse> RotateSecretAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        _ = await GetApplicationAsync(applicationId, cancellationToken);
        var webhook = await GetWebhookAsync(applicationId, cancellationToken);
        var preparingAt = DateTimeOffset.UtcNow;
        var beforePreparingJson = SafeAuditPayload(webhook);
        webhook.PrepareSecretRotation(preparingAt);
        await RecordChangeAsync(
            webhook,
            "application.webhook_secret_rotation_started",
            beforePreparingJson,
            preparingAt,
            cancellationToken);
        await SaveChangesAsync(cancellationToken);

        var signingSecret = await provider.RotateSecretAsync(
            webhook.ProviderApplicationId,
            webhook.ProviderEndpointId,
            cancellationToken);
        var rotatedAt = DateTimeOffset.UtcNow;
        var beforeJson = SafeAuditPayload(webhook);
        webhook.CompleteSecretRotation(rotatedAt);
        await RecordChangeAsync(
            webhook,
            "application.webhook_secret_rotated",
            beforeJson,
            rotatedAt,
            cancellationToken);
        await SaveChangesAsync(cancellationToken);
        return new ApplicationWebhookSecretResponse(signingSecret, rotatedAt);
    }

    private async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await store.SaveChangesAsync(cancellationToken);
        }
        catch (DataConcurrencyException)
        {
            throw new RequestConflictException("Webhook 配置已被其他请求修改，请刷新后重试。");
        }
    }

    private async Task<ApplicationEntity> GetApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return await store.GetApplicationAsync(applicationId, cancellationToken)
            ?? throw new ResourceNotFoundException("应用不存在。");
    }

    private async Task<ApplicationWebhook> GetWebhookAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return await store.GetByApplicationAsync(applicationId, cancellationToken)
            ?? throw new ResourceNotFoundException("应用尚未配置 Webhook。");
    }

    private async Task RecordChangeAsync(
        ApplicationWebhook webhook,
        string action,
        string? beforeJson,
        DateTimeOffset changedAt,
        CancellationToken cancellationToken)
    {
        var afterJson = SafeAuditPayload(webhook)!;
        await operationalFactService.RecordAuditAsync(
            new AuditEntry(
                webhook.TenantId,
                webhook.ApplicationId,
                null,
                "admin",
                null,
                action,
                "application_webhook",
                webhook.Id.ToString(),
                beforeJson,
                afterJson,
                null,
                changedAt),
            cancellationToken);
        await operationalFactService.EnqueueAsync(
            new OutboxMessage(
                action,
                "application_webhook",
                webhook.Id,
                webhook.TenantId,
                webhook.ApplicationId,
                afterJson,
                changedAt),
            cancellationToken);
    }

    private static string ValidateEndpointUrl(string endpointUrl)
    {
        var candidate = endpointUrl?.Trim();
        if (string.IsNullOrWhiteSpace(candidate) ||
            candidate.Length > 2048 ||
            !Uri.TryCreate(candidate, UriKind.Absolute, out var uri) ||
            !string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase) ||
            uri.HostNameType != UriHostNameType.Dns ||
            !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) ||
            !string.IsNullOrEmpty(uri.Fragment))
        {
            throw new RequestValidationException("Webhook 地址必须是有效的 HTTPS 地址，且不能包含凭据、查询参数或片段。");
        }

        var host = uri.DnsSafeHost.TrimEnd('.').ToLowerInvariant();
        if (host == "localhost" ||
            host.EndsWith(".localhost", StringComparison.Ordinal) ||
            host.EndsWith(".local", StringComparison.Ordinal) ||
            host.EndsWith(".internal", StringComparison.Ordinal))
        {
            throw new RequestValidationException("Webhook 地址不能指向本机或内部网络名称。");
        }

        return uri.AbsoluteUri;
    }

    private static ApplicationWebhookResponse ToResponse(
        Guid applicationId,
        ApplicationWebhook? webhook)
    {
        if (webhook is null)
        {
            return new ApplicationWebhookResponse(
                false,
                null,
                applicationId,
                null,
                false,
                null,
                false,
                null,
                null,
                null,
                null,
                null,
                null);
        }

        return new ApplicationWebhookResponse(
            true,
            webhook.Id,
            webhook.ApplicationId,
            webhook.EndpointUrl,
            webhook.IsEnabled,
            webhook.Revision,
            webhook.HasSuccessfulCurrentTest,
            webhook.LastTestId,
            webhook.LastTestId is null
                ? null
                : webhook.LastTestOutcome switch
                {
                    WebhookTestOutcome.Succeeded => WebhookTestStatus.Succeeded,
                    WebhookTestOutcome.Failed => WebhookTestStatus.Failed,
                    _ => WebhookTestStatus.Pending
                },
            webhook.LastTestHttpStatusCode,
            webhook.LastTestLatencyMilliseconds,
            webhook.LastTestedAt,
            webhook.UpdatedAt);
    }

    private static ApplicationWebhookTestResponse ToTestResponse(WebhookPublication publication)
    {
        var status = publication.Status switch
        {
            WebhookPublicationStatus.Queued => WebhookTestStatus.Pending,
            WebhookPublicationStatus.Delivering or WebhookPublicationStatus.ProviderAccepted =>
                WebhookTestStatus.Delivering,
            WebhookPublicationStatus.Succeeded => WebhookTestStatus.Succeeded,
            WebhookPublicationStatus.Failed or WebhookPublicationStatus.DeadLetter =>
                WebhookTestStatus.Failed,
            _ => throw new ArgumentOutOfRangeException(nameof(publication))
        };
        return new ApplicationWebhookTestResponse(
            publication.Id,
            publication.ApplicationId,
            publication.ConfigurationRevision,
            status,
            publication.ResponseStatusCode,
            publication.ResponseLatencyMilliseconds,
            publication.LastErrorCode,
            publication.CreatedAt,
            publication.CompletedAt);
    }

    private static string? SafeAuditPayload(ApplicationWebhook? webhook)
    {
        if (webhook is null)
        {
            return null;
        }

        var endpointHost = Uri.TryCreate(webhook.EndpointUrl, UriKind.Absolute, out var endpoint)
            ? endpoint.DnsSafeHost
            : null;
        return JsonSerializer.Serialize(
            new
            {
                webhook.Id,
                webhook.ApplicationId,
                endpointHost,
                webhook.Revision,
                webhook.IsEnabled,
                webhook.LastTestOutcome,
                webhook.LastTestedAt
            },
            JsonOptions);
    }
}

/// <summary>把已启用应用的异步终态转换为不含原文的 Webhook 事件。</summary>
public sealed class WebhookPublicationService(IApplicationWebhookStore store)
    : IWebhookPublicationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public async Task EnqueueModerationTerminalAsync(
        ModerationRequest request,
        CancellationToken cancellationToken)
    {
        var eventType = request.ProcessingStatus switch
        {
            ModerationProcessingStatus.Completed or ModerationProcessingStatus.CompletedWithErrors =>
                "moderation.completed",
            ModerationProcessingStatus.Failed => "moderation.failed",
            ModerationProcessingStatus.Cancelled => "moderation.cancelled",
            _ => null
        };
        if (eventType is null)
        {
            return;
        }

        var webhook = await store.GetByApplicationAsync(request.ApplicationId, cancellationToken);
        if (webhook is not { IsEnabled: true })
        {
            return;
        }

        var eventId = Guid.CreateVersion7();
        var occurredAt = request.FinalizedAt ?? DateTimeOffset.UtcNow;
        var payload = JsonSerializer.Serialize(
            new
            {
                schemaVersion = "1.0",
                eventId,
                eventType,
                occurredAt,
                data = new
                {
                    applicationId = request.ApplicationId,
                    requestId = request.Id,
                    processingStatus = ToWireStatus(request.ProcessingStatus),
                    statusUrl = $"/api/v1/moderation/batches/{request.Id}",
                    request.SubmittedAt,
                    request.FinalizedAt,
                    summary = new
                    {
                        itemCount = request.Items.Count,
                        passCount = request.Items.Count(item => item.Decision == ModerationDecision.Pass),
                        rejectCount = request.Items.Count(item => item.Decision == ModerationDecision.Reject),
                        reviewCount = request.Items.Count(item => item.Decision == ModerationDecision.Review),
                        failedCount = request.Items.Count(item =>
                            item.ProcessingStatus == ModerationProcessingStatus.Failed),
                        cancelledCount = request.Items.Count(item =>
                            item.ProcessingStatus == ModerationProcessingStatus.Cancelled)
                    }
                }
            },
            JsonOptions);
        var publication = new WebhookPublication(
            eventId,
            request.TenantId,
            request.ApplicationId,
            webhook.Id,
            webhook.Revision,
            webhook.ProviderApplicationId,
            webhook.ProviderEndpointId,
            WebhookPublicationKind.Notification,
            eventType,
            payload,
            $"moderation-terminal:{request.Id:N}",
            occurredAt);
        await store.AddPublicationAsync(publication, cancellationToken);
    }

    private static string ToWireStatus(ModerationProcessingStatus status)
    {
        return status switch
        {
            ModerationProcessingStatus.Completed => "completed",
            ModerationProcessingStatus.CompletedWithErrors => "completed_with_errors",
            ModerationProcessingStatus.Failed => "failed",
            ModerationProcessingStatus.Cancelled => "cancelled",
            _ => throw new ArgumentOutOfRangeException(nameof(status), status, null)
        };
    }
}
