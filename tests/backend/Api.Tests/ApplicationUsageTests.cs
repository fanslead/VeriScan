using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Tests;

public sealed class ApplicationUsageTests
{
    private static readonly string[] DefaultScopes = ["moderation:submit", "moderation:read"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task UsageEndpointRequiresAdminBearer()
    {
        await using var factory = new ApiTestFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync(
            $"/api/admin/v1/applications/{Guid.CreateVersion7()}/usage");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task UsageAggregatesStoredFactsAndFiltersByApiKey()
    {
        await using var factory = new ApiTestFactory();
        await factory.SeedRulesAsync();
        var aiClient = factory.Services.GetRequiredService<IModerationAiClient>() as TestModerationAiClient;
        Assert.NotNull(aiClient);

        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "用量统计应用");
        var firstKey = await CreateApiKeyAsync(client, application.Id, "第一把凭证");
        var secondKey = await CreateApiKeyAsync(client, application.Id, "第二把凭证");

        aiClient.Result = new AiModerationResult(
            AiModerationOutcome.Succeeded,
            AiModerationLabel.Safe,
            ["MODEL_SAFE"],
            [],
            [],
            "usage-test@1",
            "provider-1",
            32,
            18,
            null);
        await SubmitBatchAsync(client, firstKey.ApiKey, new[]
        {
            new { id = "safe-1", content = "普通内容", contentType = "plain_text" },
            new { id = "reject-1", content = "这是赌博内容", contentType = "plain_text" }
        });

        aiClient.Result = new AiModerationResult(
            AiModerationOutcome.ProviderRefusal,
            null,
            [],
            [],
            [],
            "usage-test@1",
            "provider-2",
            5,
            7,
            "AI_PROVIDER_REFUSAL");
        await SubmitBatchAsync(client, firstKey.ApiKey, new[]
        {
            new { id = "review-1", content = "另一段普通内容", contentType = "plain_text" }
        });

        await SubmitBatchAsync(client, secondKey.ApiKey, new[]
        {
            new { id = "reject-2", content = "又是赌博内容", contentType = "plain_text" }
        });

        var from = DateTimeOffset.UtcNow.AddMinutes(-5);
        var through = DateTimeOffset.UtcNow.AddMinutes(5);
        var allUsage = await GetUsageAsync(client, application.Id, from, through);
        Assert.Equal(application.Id, allUsage.ApplicationId);
        Assert.Null(allUsage.ApiKeyId);
        Assert.Equal(3, allUsage.RequestCount);
        Assert.Equal(4, allUsage.ItemCount);
        Assert.Equal(1, allUsage.PassCount);
        Assert.Equal(2, allUsage.RejectCount);
        Assert.Equal(1, allUsage.ReviewCount);
        Assert.Equal(2, allUsage.AiCallCount);
        Assert.Equal(37, allUsage.AiInputTokens);
        Assert.Equal(25, allUsage.AiOutputTokens);
        Assert.Equal(1, allUsage.AiFailureCount);
        Assert.Equal(from.ToUniversalTime(), allUsage.DataFrom);
        Assert.Equal(through.ToUniversalTime(), allUsage.DataThrough);

        var firstKeyUsage = await GetUsageAsync(
            client,
            application.Id,
            from,
            through,
            firstKey.KeyId);
        Assert.Equal(firstKey.KeyId, firstKeyUsage.ApiKeyId);
        Assert.Equal(2, firstKeyUsage.RequestCount);
        Assert.Equal(3, firstKeyUsage.ItemCount);
        Assert.Equal(1, firstKeyUsage.PassCount);
        Assert.Equal(1, firstKeyUsage.RejectCount);
        Assert.Equal(1, firstKeyUsage.ReviewCount);
        Assert.Equal(2, firstKeyUsage.AiCallCount);
        Assert.Equal(37, firstKeyUsage.AiInputTokens);
        Assert.Equal(25, firstKeyUsage.AiOutputTokens);
        Assert.Equal(1, firstKeyUsage.AiFailureCount);

        var secondKeyUsage = await GetUsageAsync(
            client,
            application.Id,
            from,
            through,
            secondKey.KeyId);
        Assert.Equal(1, secondKeyUsage.RequestCount);
        Assert.Equal(1, secondKeyUsage.ItemCount);
        Assert.Equal(0, secondKeyUsage.PassCount);
        Assert.Equal(1, secondKeyUsage.RejectCount);
        Assert.Equal(0, secondKeyUsage.ReviewCount);
        Assert.Equal(0, secondKeyUsage.AiCallCount);
        Assert.Null(secondKeyUsage.AiInputTokens);
        Assert.Null(secondKeyUsage.AiOutputTokens);
        Assert.Equal(0, secondKeyUsage.AiFailureCount);
    }

    [Fact]
    public async Task UsageValidatesWindowAndApplicationKeyBoundary()
    {
        await using var factory = new ApiTestFactory();
        await factory.SeedRulesAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "用量边界应用");
        var key = await CreateApiKeyAsync(client, application.Id, "边界凭证");
        var through = DateTimeOffset.UtcNow;

        var sameTimeResponse = await GetUsageResponseAsync(
            client,
            application.Id,
            through,
            through);
        Assert.Equal(HttpStatusCode.BadRequest, sameTimeResponse.StatusCode);

        var tooLongResponse = await GetUsageResponseAsync(
            client,
            application.Id,
            through.AddDays(-91),
            through);
        Assert.Equal(HttpStatusCode.BadRequest, tooLongResponse.StatusCode);

        var unknownApplicationResponse = await GetUsageResponseAsync(
            client,
            Guid.CreateVersion7(),
            through.AddMinutes(-1),
            through);
        Assert.Equal(HttpStatusCode.NotFound, unknownApplicationResponse.StatusCode);

        var otherApplication = await CreateApplicationAsync(client, "另一个应用");
        var foreignKeyResponse = await GetUsageResponseAsync(
            client,
            otherApplication.Id,
            through.AddMinutes(-1),
            through,
            key.KeyId);
        Assert.Equal(HttpStatusCode.NotFound, foreignKeyResponse.StatusCode);
    }

    private static async Task<ApplicationUsageResponse> GetUsageAsync(
        HttpClient client,
        Guid applicationId,
        DateTimeOffset from,
        DateTimeOffset through,
        Guid? apiKeyId = null)
    {
        var response = await GetUsageResponseAsync(client, applicationId, from, through, apiKeyId);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ApplicationUsageResponse>(body, JsonOptions)!;
    }

    private static async Task<HttpResponseMessage> GetUsageResponseAsync(
        HttpClient client,
        Guid applicationId,
        DateTimeOffset from,
        DateTimeOffset through,
        Guid? apiKeyId = null)
    {
        var query = $"?from={Uri.EscapeDataString(from.ToString("O"))}" +
                    $"&through={Uri.EscapeDataString(through.ToString("O"))}";
        if (apiKeyId.HasValue)
        {
            query += $"&apiKeyId={apiKeyId.Value}";
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/v1/applications/{applicationId}/usage{query}");
        AddAdminAuthorization(request);
        return await client.SendAsync(request);
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(
        HttpClient client,
        string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/applications");
        AddAdminAuthorization(request);
        request.Content = JsonContent.Create(new { name, environment = "test" });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;
    }

    private static async Task<ApiKeyCreatedResponse> CreateApiKeyAsync(
        HttpClient client,
        Guid applicationId,
        string displayName)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        AddAdminAuthorization(request);
        request.Content = JsonContent.Create(new
        {
            displayName,
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = DefaultScopes
        });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions))!;
    }

    private static async Task SubmitBatchAsync(
        HttpClient client,
        string apiKey,
        object[] items)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new { mode = "sync", items });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
    }

    private static void AddAdminAuthorization(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
    }
}
