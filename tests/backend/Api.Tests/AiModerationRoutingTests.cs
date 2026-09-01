using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Tests;

public sealed class AiModerationRoutingTests
{
    private static readonly string[] DefaultScopes = ["moderation:submit", "moderation:read"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task UnresolvedRuleUsesAiLabelAndKeepsRiskScoreUncalibrated()
    {
        await using var factory = new ApiTestFactory();
        await factory.SeedRulesAsync();
        var aiClient = factory.Services.GetRequiredService<IModerationAiClient>() as TestModerationAiClient;
        Assert.NotNull(aiClient);
        aiClient.Result = new AiModerationResult(
            AiModerationOutcome.Succeeded,
            AiModerationLabel.Unsafe,
            ["MODEL_CONTACT_RISK"],
            [new AiModerationCategory("contact", AiCategorySeverity.High)],
            ["请私下联系"],
            "ai-model@test",
            "provider-request-1",
            32,
            18,
            null);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client);
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var request = CreateModerationRequest(apiKey, "普通但需要语义判断的文本");
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        var result = JsonSerializer.Deserialize<BatchModerationResponse>(body, JsonOptions)!;

        var item = Assert.Single(result.Results);
        Assert.Equal(ModerationDecision.Reject, item.Decision);
        Assert.Null(item.RiskScore);
        Assert.Null(item.ScoreSource);
        Assert.Equal("external_ai:ai-model@test", item.Route);
        Assert.Equal(["MODEL_CONTACT_RISK"], item.ReasonCodes);
        Assert.Equal(1, aiClient.Calls);

        using var recordsRequest = CreateAdminRequest(
            HttpMethod.Get,
            $"/api/admin/v1/moderation-records?applicationId={application.Id}");
        var recordsResponse = await client.SendAsync(recordsRequest);
        recordsResponse.EnsureSuccessStatusCode();
        var records = await recordsResponse.Content.ReadFromJsonAsync<ModerationRecordPageResponse>(JsonOptions);
        var record = Assert.Single(records!.Items);
        Assert.Equal(2, record.DetectLevel);
        Assert.Equal(["请私下联系"], record.Evidence);
        Assert.Equal("ai-model@test", record.AiConfigurationRevision);
        Assert.Equal("provider-request-1", record.ProviderRequestId);
        Assert.Equal(32, record.AiInputTokens);
        Assert.Equal(18, record.AiOutputTokens);
    }

    [Fact]
    public async Task HardRejectDoesNotCallExternalAi()
    {
        await using var factory = new ApiTestFactory();
        await factory.SeedRulesAsync();
        var aiClient = factory.Services.GetRequiredService<IModerationAiClient>() as TestModerationAiClient;
        Assert.NotNull(aiClient);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client);
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var request = CreateModerationRequest(apiKey, "这是赌博内容");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BatchModerationResponse>(JsonOptions);

        Assert.Equal(ModerationDecision.Reject, Assert.Single(result!.Results).Decision);
        Assert.Equal(0, aiClient.Calls);
    }

    [Fact]
    public async Task IdempotentReplayReturnsOriginalResultWithoutCallingAiAgain()
    {
        await using var factory = new ApiTestFactory();
        await factory.SeedRulesAsync();
        var aiClient = factory.Services.GetRequiredService<IModerationAiClient>() as TestModerationAiClient;
        Assert.NotNull(aiClient);
        aiClient.Result = new AiModerationResult(
            AiModerationOutcome.Succeeded,
            AiModerationLabel.Safe,
            ["MODEL_SAFE"],
            [],
            [],
            "ai-model@test",
            "provider-request-idempotent",
            21,
            7,
            null);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client);
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var firstRequest = CreateModerationRequest(apiKey, "需要 AI 判断的幂等文本");
        firstRequest.Headers.Add("Idempotency-Key", "moderation-idempotency-0001");
        var firstResponse = await client.SendAsync(firstRequest);
        firstResponse.EnsureSuccessStatusCode();
        var first = await firstResponse.Content.ReadFromJsonAsync<BatchModerationResponse>(JsonOptions);

        using var replayRequest = CreateModerationRequest(apiKey, "需要 AI 判断的幂等文本");
        replayRequest.Headers.Add("Idempotency-Key", "moderation-idempotency-0001");
        var replayResponse = await client.SendAsync(replayRequest);
        replayResponse.EnsureSuccessStatusCode();
        var replay = await replayResponse.Content.ReadFromJsonAsync<BatchModerationResponse>(JsonOptions);

        Assert.Equal(first!.RequestId, replay!.RequestId);
        Assert.Equal(1, aiClient.Calls);

        using var conflictRequest = CreateModerationRequest(apiKey, "同一幂等键下的不同文本");
        conflictRequest.Headers.Add("Idempotency-Key", "moderation-idempotency-0001");
        var conflictResponse = await client.SendAsync(conflictRequest);
        Assert.Equal(System.Net.HttpStatusCode.Conflict, conflictResponse.StatusCode);
        Assert.Equal(1, aiClient.Calls);
    }

    [Fact]
    public async Task BatchRunsAiCallsWithConfiguredBoundedConcurrency()
    {
        await using var factory = new ApiTestFactory();
        await factory.SeedRulesAsync();
        var aiClient = factory.Services.GetRequiredService<IModerationAiClient>() as TestModerationAiClient;
        Assert.NotNull(aiClient);
        aiClient.Delay = TimeSpan.FromMilliseconds(60);
        aiClient.Result = new AiModerationResult(
            AiModerationOutcome.Succeeded,
            AiModerationLabel.Safe,
            ["MODEL_SAFE"],
            [],
            [],
            "ai-model@test",
            null,
            12,
            4,
            null);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client);
        var apiKey = await CreateApiKeyAsync(client, application.Id);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode = "sync",
            items = Enumerable.Range(1, 8).Select(index => new
            {
                id = $"parallel-{index}",
                content = $"需要语义判断的普通文本 {index}",
                contentType = "plain_text"
            })
        });

        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();

        Assert.True(response.IsSuccessStatusCode, body);
        Assert.Equal(8, aiClient.Calls);
        Assert.InRange(aiClient.MaximumConcurrentCalls, 2, 4);
    }

    private static HttpRequestMessage CreateModerationRequest(string apiKey, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode = "sync",
            items = new[] { new { id = "one", content, contentType = "plain_text" } }
        });
        return request;
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(HttpClient client)
    {
        using var request = CreateAdminRequest(HttpMethod.Post, "/api/admin/v1/applications");
        request.Content = JsonContent.Create(new { name = "AI 路由测试", environment = "test" });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;
    }

    private static async Task<string> CreateApiKeyAsync(HttpClient client, Guid applicationId)
    {
        using var request = CreateAdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        request.Content = JsonContent.Create(new
        {
            displayName = "AI 路由测试",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = DefaultScopes
        });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions))!.ApiKey;
    }

    private static HttpRequestMessage CreateAdminRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        return request;
    }
}
