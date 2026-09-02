using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using VeriScan.Api.Workers;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;

namespace VeriScan.Api.Tests;

/// <summary>Webhook 集成测试不能与全局 MeterListener 测试并行运行。</summary>
[CollectionDefinition("Webhook API", DisableParallelization = true)]
public sealed class WebhookApiTestGroup
{
}

/// <summary>应用 Webhook 配置、测试门禁和审核终态投递契约测试。</summary>
[Collection("Webhook API")]
public sealed class ApplicationWebhookApiTests
{
    private static readonly string[] ModerationScopes = ["moderation:submit", "moderation:read"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task GetWebhookWithoutConfigurationReturnsUnconfigured()
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "未配置 Webhook");

        using var response = await SendAdminAsync(
            client,
            HttpMethod.Get,
            WebhookPath(application.Id));
        var webhook = await ReadResponseAsync<ApplicationWebhookResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(webhook.Configured);
        Assert.Equal(application.Id, webhook.ApplicationId);
        Assert.Null(webhook.EndpointUrl);
        Assert.False(webhook.Enabled);
        Assert.False(webhook.CurrentRevisionTested);
    }

    [Theory]
    [InlineData("http://hooks.example.com/veriscan")]
    [InlineData("https://hooks.example.com/veriscan?token=secret")]
    [InlineData("https://localhost/veriscan")]
    public async Task SaveRejectsUnsafeWebhookEndpoint(string endpointUrl)
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "非法地址 Webhook");

        using var response = await SaveWebhookAsync(client, application.Id, endpointUrl);
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("request_invalid", problem.GetProperty("code").GetString());
        Assert.Equal(0, provider.ConfigureCalls);
    }

    [Fact]
    public async Task FirstSaveReturnsSigningSecretAndReplayDoesNotReturnItAgain()
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "一次性密钥 Webhook");

        using var firstResponse = await SaveWebhookAsync(
            client,
            application.Id,
            "https://hooks.example.com/veriscan");
        var first = await ReadResponseAsync<ApplicationWebhookSavedResponse>(firstResponse);

        using var secondResponse = await SaveWebhookAsync(
            client,
            application.Id,
            "https://hooks.example.com/veriscan");
        var second = await ReadResponseAsync<ApplicationWebhookSavedResponse>(secondResponse);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.True(first.Webhook.Configured);
        Assert.False(first.Webhook.Enabled);
        Assert.Equal(1, first.Webhook.Revision);
        Assert.Equal(FakeWebhookProvider.InitialSigningSecret, first.SigningSecret);
        Assert.Equal(HttpStatusCode.OK, secondResponse.StatusCode);
        Assert.Null(second.SigningSecret);
        Assert.Equal(first.Webhook.Id, second.Webhook.Id);
        Assert.Equal(first.Webhook.Revision, second.Webhook.Revision);
        Assert.Equal(2, provider.ConfigureCalls);
    }

    [Fact]
    public async Task EnableBeforeCurrentRevisionTestReturnsConflict()
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "测试门禁 Webhook");
        _ = await SaveWebhookAsync(client, application.Id, "https://hooks.example.com/veriscan");

        using var response = await SetWebhookStatusAsync(client, application.Id, true);
        var problem = await ReadProblemAsync(response);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        Assert.Equal("request_conflict", problem.GetProperty("code").GetString());
        Assert.Contains("连接测试", problem.GetProperty("detail").GetString());
    }

    [Fact]
    public async Task TestReturnsAcceptedWithTestIdAndStatusUrl()
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "测试受理 Webhook");
        _ = await SaveWebhookAsync(client, application.Id, "https://hooks.example.com/veriscan");

        using var response = await SendAdminAsync(
            client,
            HttpMethod.Post,
            $"{WebhookPath(application.Id)}/tests");
        var accepted = await ReadResponseAsync<ApplicationWebhookTestAcceptedResponse>(response);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotEqual(Guid.Empty, accepted.TestId);
        Assert.Equal(
            $"/api/admin/v1/applications/{application.Id}/webhook/tests/{accepted.TestId}",
            accepted.StatusUrl);
        Assert.NotNull(response.Headers.Location);
        Assert.Equal(accepted.StatusUrl, response.Headers.Location!.OriginalString);
        Assert.Empty(provider.Published);
    }

    [Fact]
    public async Task WebhookTestIsDeliveredAndCurrentRevisionCanBeEnabled()
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider, startWebhookWorker: true);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "连通测试 Webhook");
        _ = await SaveWebhookAsync(client, application.Id, "https://hooks.example.com/veriscan");

        using var acceptedResponse = await SendAdminAsync(
            client,
            HttpMethod.Post,
            $"{WebhookPath(application.Id)}/tests");
        var accepted = await ReadResponseAsync<ApplicationWebhookTestAcceptedResponse>(acceptedResponse);

        await EventuallyAsync(
            async () =>
            {
                using var statusResponse = await SendAdminAsync(
                    client,
                    HttpMethod.Get,
                    accepted.StatusUrl);
                if (statusResponse.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                var status = await ReadResponseAsync<ApplicationWebhookTestResponse>(statusResponse);
                return status.Status == WebhookTestStatus.Succeeded &&
                       status.HttpStatusCode == 204 &&
                       status.LatencyMilliseconds == 12;
            });

        using var enableResponse = await SetWebhookStatusAsync(client, application.Id, true);
        var enabled = await ReadResponseAsync<ApplicationWebhookResponse>(enableResponse);

        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);
        Assert.True(enabled.Enabled);
        Assert.True(enabled.CurrentRevisionTested);
        Assert.Equal(accepted.TestId, enabled.LastTestId);
        var testMessage = Assert.Single(provider.Published, message => message.EventType == "webhook.test");
        Assert.DoesNotContain("审核原文", testMessage.PayloadJson, StringComparison.Ordinal);
    }

    [Fact]
    public async Task ChangingEndpointAutomaticallyDisablesAndInvalidatesPreviousTest()
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider, startWebhookWorker: true);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "地址变更 Webhook");
        _ = await SaveWebhookAsync(client, application.Id, "https://hooks.example.com/one");
        await CompleteAndEnableTestAsync(client, application.Id);

        using var response = await SaveWebhookAsync(
            client,
            application.Id,
            "https://hooks.example.com/two");
        var saved = await ReadResponseAsync<ApplicationWebhookSavedResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("https://hooks.example.com/two", saved.Webhook.EndpointUrl);
        Assert.Equal(2, saved.Webhook.Revision);
        Assert.False(saved.Webhook.Enabled);
        Assert.False(saved.Webhook.CurrentRevisionTested);
        Assert.Null(saved.Webhook.LastTestId);
        Assert.Null(saved.Webhook.LastTestStatus);
        Assert.Null(saved.SigningSecret);
    }

    [Fact]
    public async Task RotatingSecretAutomaticallyDisablesAndInvalidatesPreviousTest()
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider, startWebhookWorker: true);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "轮换密钥 Webhook");
        using var saveResponse = await SaveWebhookAsync(
            client,
            application.Id,
            "https://hooks.example.com/veriscan");
        var saved = await ReadResponseAsync<ApplicationWebhookSavedResponse>(saveResponse);
        await CompleteAndEnableTestAsync(client, application.Id);

        using var rotateResponse = await SendAdminAsync(
            client,
            HttpMethod.Post,
            $"{WebhookPath(application.Id)}/secret/rotate");
        var rotated = await ReadResponseAsync<ApplicationWebhookSecretResponse>(rotateResponse);
        using var getResponse = await SendAdminAsync(client, HttpMethod.Get, WebhookPath(application.Id));
        var current = await ReadResponseAsync<ApplicationWebhookResponse>(getResponse);

        Assert.Equal(HttpStatusCode.OK, rotateResponse.StatusCode);
        Assert.Equal(FakeWebhookProvider.RotatedSigningSecret, rotated.SigningSecret);
        Assert.NotEqual(saved.SigningSecret, rotated.SigningSecret);
        Assert.True(rotated.RotatedAt > DateTimeOffset.MinValue);
        Assert.Equal(2, current.Revision);
        Assert.False(current.Enabled);
        Assert.False(current.CurrentRevisionTested);
        Assert.Null(current.LastTestId);
        Assert.Equal(1, provider.RotateCalls);
    }

    [Fact]
    public async Task SyncBatchDoesNotPublishButAsyncTerminalPublishesThinPayload()
    {
        var provider = new FakeWebhookProvider();
        await using var factory = await CreateFactoryAsync(provider, startWebhookWorker: true);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "审核终态 Webhook");
        var apiKey = await CreateApiKeyAsync(client, application.Id);
        _ = await SaveWebhookAsync(client, application.Id, "https://hooks.example.com/veriscan");
        await CompleteAndEnableTestAsync(client, application.Id);

        const string syncContent = "同步审核不应触发 webhook 的原文";
        using var syncRequest = CreateModerationRequest(apiKey, "sync-item", syncContent, "sync");
        using var syncResponse = await client.SendAsync(syncRequest);
        var sync = await ReadResponseAsync<BatchModerationResponse>(syncResponse);

        Assert.Equal(HttpStatusCode.OK, syncResponse.StatusCode);
        Assert.Equal("completed", sync.ProcessingStatus);
        Assert.DoesNotContain(
            provider.Published,
            message => message.EventType == "moderation.completed");
        await using (var syncScope = factory.Services.CreateAsyncScope())
        {
            var syncDb = syncScope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
            Assert.DoesNotContain(
                await syncDb.WebhookPublications.AsNoTracking().ToArrayAsync(),
                publication => publication.Kind == WebhookPublicationKind.Notification);
        }

        const string asyncContent = "异步审核终态 webhook 不得包含的审核原文";
        using var asyncRequest = CreateModerationRequest(apiKey, "async-item", asyncContent, "async");
        using var asyncResponse = await client.SendAsync(asyncRequest);
        var accepted = await ReadResponseAsync<BatchModerationResponse>(asyncResponse);
        Assert.Equal(HttpStatusCode.Accepted, asyncResponse.StatusCode);

        await using (var moderationScope = factory.Services.CreateAsyncScope())
        {
            var moderationService = moderationScope.ServiceProvider.GetRequiredService<IModerationService>();
            await moderationService.ProcessQueuedBatchAsync(accepted.RequestId, CancellationToken.None);
        }

        await EventuallyAsync(
            () => Task.FromResult(provider.Published.Any(message =>
                message.EventType == "moderation.completed" &&
                message.PayloadJson.Contains(accepted.RequestId.ToString(), StringComparison.Ordinal))));

        var notification = Assert.Single(
            provider.Published,
            message => message.EventType == "moderation.completed");
        Assert.DoesNotContain(asyncContent, notification.PayloadJson, StringComparison.Ordinal);
        Assert.DoesNotContain("content", notification.PayloadJson, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("itemCount", notification.PayloadJson, StringComparison.Ordinal);
        Assert.Contains(accepted.RequestId.ToString(), notification.PayloadJson, StringComparison.Ordinal);

        await using var notificationScope = factory.Services.CreateAsyncScope();
        var notificationDb = notificationScope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
        var publication = await notificationDb.WebhookPublications
            .AsNoTracking()
            .SingleAsync(item => item.Kind == WebhookPublicationKind.Notification);
        Assert.Equal(WebhookPublicationStatus.ProviderAccepted, publication.Status);
        Assert.DoesNotContain(asyncContent, publication.PayloadJson, StringComparison.Ordinal);
    }

    private static async Task<ApiTestFactory> CreateFactoryAsync(
        FakeWebhookProvider provider,
        bool startWebhookWorker = false)
    {
        var factory = new ApiTestFactory(services =>
        {
            services.RemoveAll<IWebhookProvider>();
            services.AddSingleton<IWebhookProvider>(provider);
            services.RemoveAll<IHostedService>();
            if (startWebhookWorker)
            {
                services.PostConfigure<WebhookPublicationWorkerOptions>(options =>
                {
                    options.Enabled = true;
                    options.BatchSize = 10;
                    options.LeaseSeconds = 5;
                    options.PollDelayMilliseconds = 50;
                    options.TestPollDelayMilliseconds = 100;
                    options.TestTimeoutSeconds = 5;
                    options.MaximumPublishAttempts = 3;
                    options.MaximumFailureBackoffSeconds = 1;
                });
                services.AddHostedService<WebhookPublicationWorker>();
            }
        });
        await factory.SeedRulesAsync();
        return factory;
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(
        HttpClient client,
        string name)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/admin/v1/applications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        request.Content = JsonContent.Create(new { name, environment = "test" });
        using var response = await client.SendAsync(request);
        return await ReadResponseAsync<ApplicationResponse>(response);
    }

    private static async Task<string> CreateApiKeyAsync(HttpClient client, Guid applicationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        request.Content = JsonContent.Create(new
        {
            displayName = "Webhook 审核测试",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = ModerationScopes
        });
        using var response = await client.SendAsync(request);
        var created = await ReadResponseAsync<ApiKeyCreatedResponse>(response);
        return created.ApiKey;
    }

    private static HttpRequestMessage CreateModerationRequest(
        string apiKey,
        string itemId,
        string content,
        string mode)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode,
            items = new[]
            {
                new
                {
                    id = itemId,
                    content,
                    contentType = "plain_text"
                }
            }
        });
        return request;
    }

    private static async Task<HttpResponseMessage> SaveWebhookAsync(
        HttpClient client,
        Guid applicationId,
        string endpointUrl)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Put,
            WebhookPath(applicationId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        request.Content = JsonContent.Create(new { endpointUrl });
        return await client.SendAsync(request);
    }

    private static async Task<HttpResponseMessage> SetWebhookStatusAsync(
        HttpClient client,
        Guid applicationId,
        bool enabled)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Patch,
            WebhookPath(applicationId));
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        request.Content = JsonContent.Create(new { enabled });
        return await client.SendAsync(request);
    }

    private static async Task<ApplicationWebhookTestAcceptedResponse> CompleteAndEnableTestAsync(
        HttpClient client,
        Guid applicationId)
    {
        using var acceptedResponse = await SendAdminAsync(
            client,
            HttpMethod.Post,
            $"{WebhookPath(applicationId)}/tests");
        var accepted = await ReadResponseAsync<ApplicationWebhookTestAcceptedResponse>(acceptedResponse);
        await EventuallyAsync(
            async () =>
            {
                using var statusResponse = await SendAdminAsync(
                    client,
                    HttpMethod.Get,
                    accepted.StatusUrl);
                if (statusResponse.StatusCode != HttpStatusCode.OK)
                {
                    return false;
                }

                var status = await ReadResponseAsync<ApplicationWebhookTestResponse>(statusResponse);
                return status.Status == WebhookTestStatus.Succeeded;
            });

        using var enableResponse = await SetWebhookStatusAsync(client, applicationId, true);
        var enabled = await ReadResponseAsync<ApplicationWebhookResponse>(enableResponse);
        Assert.Equal(HttpStatusCode.OK, enableResponse.StatusCode);
        Assert.True(enabled.Enabled);
        return accepted;
    }

    private static Task<HttpResponseMessage> SendAdminAsync(
        HttpClient client,
        HttpMethod method,
        string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        return client.SendAsync(request);
    }

    private static string WebhookPath(Guid applicationId) =>
        $"/api/admin/v1/applications/{applicationId}/webhook";

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode ||
            response.StatusCode is HttpStatusCode.BadRequest or
                HttpStatusCode.Conflict or
                HttpStatusCode.NotFound or
                HttpStatusCode.ServiceUnavailable,
            $"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    private static async Task<JsonElement> ReadProblemAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.Clone();
    }

    private static async Task EventuallyAsync(
        Func<Task<bool>> predicate,
        int attempts = 80)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(50);
        }

        Assert.Fail("等待 Webhook 后台状态完成超时。");
    }

    public sealed record PublishedWebhook(
        string ProviderApplicationId,
        Guid EventId,
        string EventType,
        string PayloadJson);

    private sealed class FakeWebhookProvider : IWebhookProvider
    {
        private readonly ConcurrentQueue<PublishedWebhook> published = new();

        public const string InitialSigningSecret = "whsec_initial_test_secret";

        public const string RotatedSigningSecret = "whsec_rotated_test_secret";

        public int ConfigureCalls;

        public int RotateCalls;

        public IReadOnlyList<PublishedWebhook> Published => published.ToArray();

        public Task<WebhookEndpointRegistration> ConfigureEndpointAsync(
            Guid applicationId,
            string applicationName,
            string endpointUrl,
            bool revealSecret,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref ConfigureCalls);
            return Task.FromResult(
                new WebhookEndpointRegistration(
                    $"fake-app-{applicationId:N}",
                    "primary",
                    revealSecret ? InitialSigningSecret : null));
        }

        public Task<string> RotateSecretAsync(
            string providerApplicationId,
            string providerEndpointId,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref RotateCalls);
            return Task.FromResult(RotatedSigningSecret);
        }

        public Task<WebhookPublishReceipt> PublishAsync(
            string providerApplicationId,
            Guid eventId,
            string eventType,
            string payloadJson,
            CancellationToken cancellationToken)
        {
            published.Enqueue(new PublishedWebhook(
                providerApplicationId,
                eventId,
                eventType,
                payloadJson));
            return Task.FromResult(new WebhookPublishReceipt($"fake-message-{eventId:N}"));
        }

        public Task<WebhookAttemptResult> GetAttemptAsync(
            string providerApplicationId,
            string providerMessageId,
            string providerEndpointId,
            CancellationToken cancellationToken)
        {
            return Task.FromResult(
                new WebhookAttemptResult(
                    WebhookAttemptState.Succeeded,
                    204,
                    12,
                    null));
        }
    }
}
