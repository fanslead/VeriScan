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
