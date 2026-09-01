using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;

namespace VeriScan.Api.Tests;

/// <summary>异步审核批次的 HTTP 契约和生命周期测试。</summary>
public sealed class ModerationAsyncApiTests
{
    private static readonly string[] ModerationScopes = ["moderation:submit", "moderation:read"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task AsyncSubmissionReturnsAcceptedWithLocationAndRetryAfter()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "异步受理");
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var request = CreateModerationRequest(
            apiKey,
            "async-item",
            "等待异步审核的普通文本",
            "async");
        var response = await client.SendAsync(request);
        var body = await ReadResponseAsync<BatchModerationResponse>(response);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        Assert.NotNull(response.Headers.Location);
        Assert.EndsWith(
            $"/api/v1/moderation/batches/{body.RequestId}",
            response.Headers.Location!.OriginalString,
            StringComparison.Ordinal);
        Assert.Equal("2", response.Headers.GetValues("Retry-After").Single());
        Assert.Equal("accepted", body.ProcessingStatus);
        var item = Assert.Single(body.Results);
        Assert.Equal("async-item", item.Id);
        Assert.Equal("accepted", item.ProcessingStatus);
        Assert.Null(item.Decision);
    }

    [Fact]
    public async Task GetBatchOnlyReturnsTheBatchForTheOwningApplication()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var ownerApplication = await CreateApplicationAsync(client, "批次所属应用");
        var ownerKey = await CreateApiKeyAsync(client, ownerApplication.Id);
        var otherApplication = await CreateApplicationAsync(client, "其他应用");
        var otherKey = await CreateApiKeyAsync(client, otherApplication.Id);

        using var submit = CreateModerationRequest(
            ownerKey,
            "owner-item",
            "仅属于第一个应用的异步内容",
            "async");
        var submitResponse = await client.SendAsync(submit);
        var submitted = await ReadResponseAsync<BatchModerationResponse>(submitResponse);
        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);

        using var ownerRead = CreateApiRequest(
            HttpMethod.Get,
            $"/api/v1/moderation/batches/{submitted.RequestId}",
            ownerKey);
        var ownerReadResponse = await client.SendAsync(ownerRead);
        Assert.Equal(HttpStatusCode.OK, ownerReadResponse.StatusCode);
        var ownerBatch = await ReadResponseAsync<BatchModerationResponse>(ownerReadResponse);
        Assert.Equal(submitted.RequestId, ownerBatch.RequestId);

        using var otherRead = CreateApiRequest(
            HttpMethod.Get,
            $"/api/v1/moderation/batches/{submitted.RequestId}",
            otherKey);
        var otherReadResponse = await client.SendAsync(otherRead);
        Assert.Equal(HttpStatusCode.NotFound, otherReadResponse.StatusCode);
    }

    [Fact]
    public async Task CancelPendingBatchMovesBatchAndItemsToCancelledTerminalState()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "取消异步批次");
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var submit = CreateModerationRequest(
            apiKey,
            "cancel-item",
            "提交后尚未开始处理的内容",
            "async");
        var submitResponse = await client.SendAsync(submit);
        var submitted = await ReadResponseAsync<BatchModerationResponse>(submitResponse);
        Assert.Equal(HttpStatusCode.Accepted, submitResponse.StatusCode);

        using var cancel = CreateApiRequest(
            HttpMethod.Post,
            $"/api/v1/moderation/batches/{submitted.RequestId}/cancel",
            apiKey);
        var cancelResponse = await client.SendAsync(cancel);
        var cancelled = await ReadResponseAsync<BatchModerationResponse>(cancelResponse);

        Assert.Equal(HttpStatusCode.OK, cancelResponse.StatusCode);
        Assert.Equal(submitted.RequestId, cancelled.RequestId);
        Assert.Equal("cancelled", cancelled.ProcessingStatus);
        Assert.NotNull(cancelled.FinalizedAt);
        var cancelledItem = Assert.Single(cancelled.Results);
        Assert.Equal("cancelled", cancelledItem.ProcessingStatus);
        Assert.Null(cancelledItem.Decision);
        Assert.NotNull(cancelledItem.FinalizedAt);

        using var read = CreateApiRequest(
            HttpMethod.Get,
            $"/api/v1/moderation/batches/{submitted.RequestId}",
            apiKey);
        var readResponse = await client.SendAsync(read);
        var persisted = await ReadResponseAsync<BatchModerationResponse>(readResponse);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal("cancelled", persisted.ProcessingStatus);
        Assert.Equal("cancelled", Assert.Single(persisted.Results).ProcessingStatus);
    }

    [Fact]
    public async Task AutoModeUsesSynchronousPathBelowConfiguredThresholdAndQueuesAboveIt()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var aiClient = Assert.IsType<TestModerationAiClient>(
            factory.Services.GetRequiredService<IModerationAiClient>());
        aiClient.Result = SuccessfulSafeResult();
        var application = await CreateApplicationAsync(client, "自动路由");
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var smallRequest = CreateModerationRequest(
            apiKey,
            "auto-sync-item",
            "低于自动异步阈值的内容",
            "auto");
        var smallResponse = await client.SendAsync(smallRequest);
        var small = await ReadResponseAsync<BatchModerationResponse>(smallResponse);

        Assert.Equal(HttpStatusCode.OK, smallResponse.StatusCode);
        Assert.Equal("completed", small.ProcessingStatus);
        Assert.Equal("completed", Assert.Single(small.Results).ProcessingStatus);
        Assert.Equal(1, aiClient.Calls);

        using var largeRequest = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/moderation/batches");
        largeRequest.Headers.Add("X-API-Key", apiKey);
        largeRequest.Content = JsonContent.Create(new
        {
            mode = "auto",
            items = Enumerable.Range(1, 11).Select(index => new
            {
                id = $"auto-async-{index}",
                content = $"超过自动异步条数阈值的内容 {index}",
                contentType = "plain_text"
            })
        });
        var largeResponse = await client.SendAsync(largeRequest);
        var large = await ReadResponseAsync<BatchModerationResponse>(largeResponse);

        Assert.Equal(HttpStatusCode.Accepted, largeResponse.StatusCode);
        Assert.Equal("accepted", large.ProcessingStatus);
        Assert.Equal(11, large.Results.Count);
        Assert.All(large.Results, item => Assert.Equal("accepted", item.ProcessingStatus));
        Assert.Equal(1, aiClient.Calls);
    }

    [Fact]
    public async Task SyncBatchPreservesCallerItemOrder()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var aiClient = Assert.IsType<TestModerationAiClient>(
            factory.Services.GetRequiredService<IModerationAiClient>());
        aiClient.Result = SuccessfulSafeResult();
        var application = await CreateApplicationAsync(client, "结果顺序");
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode = "sync",
            items = new[]
            {
                new { id = "caller-03", content = "第三条内容", contentType = "plain_text" },
                new { id = "caller-01", content = "第一条内容", contentType = "plain_text" },
                new { id = "caller-02", content = "第二条内容", contentType = "plain_text" }
            }
        });

        var response = await client.SendAsync(request);
        var result = await ReadResponseAsync<BatchModerationResponse>(response);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(
            ["caller-03", "caller-01", "caller-02"],
            result.Results.Select(item => item.Id).ToArray());
    }

    [Fact]
    public async Task SyncDeadlineReturnsRequestTimeoutAndPersistsFailedBatch()
    {
        await using var factory = await CreateFactoryAsync(services =>
        {
            services.RemoveAll<IModerationExecutionPolicy>();
            services.AddSingleton<IModerationExecutionPolicy, ShortDeadlineExecutionPolicy>();
            services.RemoveAll<IModerationAiClient>();
            services.AddSingleton<IModerationAiClient, SlowCancellationAwareAiClient>();
        });
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "同步截止时间");
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var request = CreateModerationRequest(
            apiKey,
            "deadline-item",
            "等待慢速模型导致同步超时的文本",
            "sync");
        var response = await client.SendAsync(request);
        var problemBody = await response.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.GatewayTimeout, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var problem = JsonDocument.Parse(problemBody);
        Assert.Equal("request_timeout", problem.RootElement.GetProperty("code").GetString());

        Guid requestId;
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
            var persisted = await dbContext.ModerationRequests
                .Include(moderationRequest => moderationRequest.Items)
                .SingleAsync(moderationRequest => moderationRequest.ApplicationId == application.Id);
            requestId = persisted.Id;

            Assert.Equal(ModerationProcessingStatus.Failed, persisted.ProcessingStatus);
            var item = Assert.Single(persisted.Items);
            Assert.Equal(ModerationProcessingStatus.Failed, item.ProcessingStatus);
            Assert.Equal("SYNC_DEADLINE_EXCEEDED", item.ErrorCode);
            Assert.Null(item.Decision);
            Assert.NotNull(persisted.FinalizedAt);
            Assert.NotNull(item.FinalizedAt);
        }

        using var read = CreateApiRequest(
            HttpMethod.Get,
            $"/api/v1/moderation/batches/{requestId}",
            apiKey);
        var readResponse = await client.SendAsync(read);
        var failedBatch = await ReadResponseAsync<BatchModerationResponse>(readResponse);

        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal(requestId, failedBatch.RequestId);
        Assert.Equal("failed", failedBatch.ProcessingStatus);
        var failedItem = Assert.Single(failedBatch.Results);
        Assert.Equal("failed", failedItem.ProcessingStatus);
        Assert.Equal("SYNC_DEADLINE_EXCEEDED", failedItem.ErrorCode);
        Assert.Null(failedItem.Decision);
    }

    [Fact]
    public async Task SameIdempotencyKeyReplayWhileProcessingReturnsTheOriginalAcceptedBatch()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var aiClient = Assert.IsType<TestModerationAiClient>(
            factory.Services.GetRequiredService<IModerationAiClient>());
        var application = await CreateApplicationAsync(client, "处理中重放");
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var firstRequest = CreateModerationRequest(
            apiKey,
            "idempotent-item",
            "仍在队列中的幂等请求",
            "async");
        firstRequest.Headers.Add("Idempotency-Key", "async-replay-key-0001");
        var firstResponse = await client.SendAsync(firstRequest);
        var first = await ReadResponseAsync<BatchModerationResponse>(firstResponse);

        using var replayRequest = CreateModerationRequest(
            apiKey,
            "idempotent-item",
            "仍在队列中的幂等请求",
            "async");
        replayRequest.Headers.Add("Idempotency-Key", "async-replay-key-0001");
        var replayResponse = await client.SendAsync(replayRequest);
        var replay = await ReadResponseAsync<BatchModerationResponse>(replayResponse);

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Accepted, replayResponse.StatusCode);
        Assert.Equal(first.RequestId, replay.RequestId);
        Assert.Equal("accepted", replay.ProcessingStatus);
        Assert.NotNull(replayResponse.Headers.Location);
        Assert.EndsWith(
            $"/api/v1/moderation/batches/{first.RequestId}",
            replayResponse.Headers.Location!.OriginalString,
            StringComparison.Ordinal);
        Assert.Equal("2", replayResponse.Headers.GetValues("Retry-After").Single());
        Assert.Equal(0, aiClient.Calls);
    }

    [Fact]
    public async Task DifferentRequestWithTheSameIdempotencyKeyReturnsConflict()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "幂等冲突");
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var firstRequest = CreateModerationRequest(
            apiKey,
            "first-item",
            "第一个幂等请求",
            "async");
        firstRequest.Headers.Add("Idempotency-Key", "async-conflict-key-0001");
        var firstResponse = await client.SendAsync(firstRequest);
        var first = await ReadResponseAsync<BatchModerationResponse>(firstResponse);

        using var conflictRequest = CreateModerationRequest(
            apiKey,
            "different-item",
            "完全不同的幂等请求",
            "async");
        conflictRequest.Headers.Add("Idempotency-Key", "async-conflict-key-0001");
        var conflictResponse = await client.SendAsync(conflictRequest);

        Assert.Equal(HttpStatusCode.Accepted, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal("application/problem+json", conflictResponse.Content.Headers.ContentType?.MediaType);

        using var read = CreateApiRequest(
            HttpMethod.Get,
            $"/api/v1/moderation/batches/{first.RequestId}",
            apiKey);
        var readResponse = await client.SendAsync(read);
        var persisted = await ReadResponseAsync<BatchModerationResponse>(readResponse);
        Assert.Equal(HttpStatusCode.OK, readResponse.StatusCode);
        Assert.Equal(first.RequestId, persisted.RequestId);
        Assert.Equal("accepted", persisted.ProcessingStatus);
        Assert.Equal("first-item", Assert.Single(persisted.Results).Id);
    }

    private static async Task<ApiTestFactory> CreateFactoryAsync(
        Action<IServiceCollection>? additionalServices = null)
    {
        var factory = new ApiTestFactory(services =>
        {
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
            additionalServices?.Invoke(services);
        });
        await factory.SeedRulesAsync();
        return factory;
    }

    private static HttpRequestMessage CreateModerationRequest(
        string apiKey,
        string itemId,
        string content,
        string mode)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode,
            items = new[] { new { id = itemId, content, contentType = "plain_text" } }
        });
        return request;
    }

    private static HttpRequestMessage CreateApiRequest(
        HttpMethod method,
        string uri,
        string apiKey)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Add("X-API-Key", apiKey);
        return request;
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(
        HttpClient client,
        string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/applications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        request.Content = JsonContent.Create(new { name, environment = "test" });
        var response = await client.SendAsync(request);
        return await ReadResponseAsync<ApplicationResponse>(response);
    }

    private static async Task<string> CreateApiKeyAsync(
        HttpClient client,
        Guid applicationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        request.Content = JsonContent.Create(new
        {
            displayName = "异步 API 测试",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = ModerationScopes
        });
        var response = await client.SendAsync(request);
        return (await ReadResponseAsync<ApiKeyCreatedResponse>(response)).ApiKey;
    }

    private static AiModerationResult SuccessfulSafeResult()
    {
        return new AiModerationResult(
            AiModerationOutcome.Succeeded,
            AiModerationLabel.Safe,
            ["MODEL_SAFE"],
            [],
            [],
            "ai-model@test-safe",
            "provider-safe",
            8,
            2,
            null);
    }

    private static async Task<T> ReadResponseAsync<T>(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(
            response.IsSuccessStatusCode || response.StatusCode == HttpStatusCode.Conflict ||
            response.StatusCode == HttpStatusCode.NotFound,
            $"HTTP {(int)response.StatusCode}: {body}");
        return JsonSerializer.Deserialize<T>(body, JsonOptions)!;
    }

    private sealed class ShortDeadlineExecutionPolicy : IModerationExecutionPolicy
    {
        public int MaximumConcurrentAiCalls => 1;

        public TimeSpan SynchronousDeadline => TimeSpan.FromMilliseconds(50);
    }

    private sealed class SlowCancellationAwareAiClient : IModerationAiClient
    {
        public async Task<AiModerationResult> ModerateAsync(
            AiModerationRequest request,
            CancellationToken cancellationToken)
        {
            await Task.Delay(Timeout.InfiniteTimeSpan, cancellationToken);
            return SuccessfulSafeResult();
        }
    }
}
