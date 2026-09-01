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

public sealed class ModerationApiTests : IClassFixture<ApiTestFactory>
{
    private static readonly string[] DefaultScopes = ["moderation:submit", "moderation:read"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly ApiTestFactory factory;

    public ModerationApiTests(ApiTestFactory factory)
    {
        this.factory = factory;
        factory.SeedRulesAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task ApiKeyCanSubmitModerationAndPersistsResults()
    {
        var aiClient = Assert.IsType<TestModerationAiClient>(
            factory.Services.GetRequiredService<IModerationAiClient>());
        aiClient.Handler = aiRequest => new AiModerationResult(
            AiModerationOutcome.Succeeded,
            aiRequest.Content.Contains("加微信", StringComparison.Ordinal)
                ? AiModerationLabel.Review
                : AiModerationLabel.Safe,
            ["MODEL_DECISION"],
            [],
            [],
            "ai-model@test-safe",
            "provider-safe",
            10,
            3,
            null);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "内容服务");
        var key = await CreateApiKeyAsync(client, application.Id);

        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", key.ApiKey);
        request.Content = JsonContent.Create(new
        {
            mode = "sync",
            items = new[]
            {
                new { id = "safe", content = "明鉴内容服务", contentType = "plain_text" },
                new { id = "blocked", content = "这是赌博内容", contentType = "plain_text" },
                new { id = "uncertain", content = "请加微信联系", contentType = "plain_text" }
            }
        });

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var result = await response.Content.ReadFromJsonAsync<BatchModerationResponse>(JsonOptions);
        Assert.NotNull(result);
        Assert.Equal(3, result.Results.Count);
        Assert.Equal(ModerationDecision.Pass, result.Results[0].Decision);
        Assert.Equal(ModerationDecision.Reject, result.Results[1].Decision);
        Assert.Equal(ModerationDecision.Review, result.Results[2].Decision);
        Assert.True(result.Results[2].ReviewRequired);
        Assert.Equal("ai_model", result.Results[2].ReviewSource);

        using var getRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/v1/moderation/batches/{result.RequestId}");
        getRequest.Headers.Add("X-API-Key", key.ApiKey);
        var getResponse = await client.SendAsync(getRequest);
        Assert.Equal(HttpStatusCode.OK, getResponse.StatusCode);

        using var keysRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/v1/applications/{application.Id}/api-keys");
        AddAdminAuthorization(keysRequest);
        var keysResponse = await client.SendAsync(keysRequest);
        keysResponse.EnsureSuccessStatusCode();
        var keys = await keysResponse.Content.ReadFromJsonAsync<ApiKeyListResponse>(JsonOptions);
        Assert.NotNull(Assert.Single(keys!.Items).LastUsedAt);
    }

    [Fact]
    public async Task InvalidAndRevokedApiKeysReturnSameUnauthorizedShape()
    {
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "Key 生命周期");
        var key = await CreateApiKeyAsync(client, application.Id);

        var replacement = key.ApiKey[^1] == 'a' ? 'b' : 'a';
        using var invalidRequest = CreateModerationRequest(key.ApiKey[..^1] + replacement);
        var invalidResponse = await client.SendAsync(invalidRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, invalidResponse.StatusCode);
        Assert.Equal("application/problem+json", invalidResponse.Content.Headers.ContentType?.MediaType);

        using var revokeRequest = new HttpRequestMessage(
            HttpMethod.Delete,
            $"/api/admin/v1/applications/{application.Id}/api-keys/{key.KeyId}");
        AddAdminAuthorization(revokeRequest);
        var revokeResponse = await client.SendAsync(revokeRequest);
        Assert.Equal(HttpStatusCode.NoContent, revokeResponse.StatusCode);

        using var revokedRequest = CreateModerationRequest(key.ApiKey);
        var revokedResponse = await client.SendAsync(revokedRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, revokedResponse.StatusCode);
        Assert.Equal("application/problem+json", revokedResponse.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task ManagementApiWithoutAdminBearerReturnsUnauthorized()
    {
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/api/admin/v1/applications");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ManagementApiRejectsApplicationApiKey()
    {
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "管理边界");
        var key = await CreateApiKeyAsync(client, application.Id);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/v1/applications");
        request.Headers.Add("X-API-Key", key.ApiKey);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task OpenApiDocumentsApiKeyHeader()
    {
        using var client = factory.CreateClient();

        var document = await client.GetStringAsync("/openapi/v1.json");

        Assert.Contains("X-API-Key", document, StringComparison.Ordinal);
        Assert.Contains("bearer", document, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("/api/v1/moderation/batches", document, StringComparison.Ordinal);
        Assert.Contains("/api/admin/v1/overview", document, StringComparison.Ordinal);

        using var json = JsonDocument.Parse(document);
        var schemes = json.RootElement.GetProperty("components").GetProperty("securitySchemes");
        Assert.True(schemes.TryGetProperty("ApiKey", out var apiKeyScheme));
        Assert.Equal("apiKey", apiKeyScheme.GetProperty("type").GetString());
        Assert.Equal("X-API-Key", apiKeyScheme.GetProperty("name").GetString());
        Assert.True(schemes.TryGetProperty("Bearer", out var bearerScheme));
        Assert.Equal("http", bearerScheme.GetProperty("type").GetString());
        Assert.Equal("bearer", bearerScheme.GetProperty("scheme").GetString());

        var paths = json.RootElement.GetProperty("paths");
        var adminSecurity = paths
            .GetProperty("/api/admin/v1/overview")
            .GetProperty("get")
            .GetProperty("security");
        Assert.True(adminSecurity[0].TryGetProperty("Bearer", out _));

        var moderationSecurity = paths
            .GetProperty("/api/v1/moderation/batches")
            .GetProperty("post")
            .GetProperty("security");
        Assert.True(moderationSecurity[0].TryGetProperty("ApiKey", out _));
        Assert.False(moderationSecurity[0].TryGetProperty("Bearer", out _));
    }

    private static HttpRequestMessage CreateModerationRequest(string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode = "sync",
            items = new[] { new { id = "one", content = "普通文本", contentType = "plain_text" } }
        });
        return request;
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(HttpClient client, string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/applications");
        AddAdminAuthorization(request);
        request.Content = JsonContent.Create(new { name, environment = "test" });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;
    }

    private static async Task<ApiKeyCreatedResponse> CreateApiKeyAsync(HttpClient client, Guid applicationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        AddAdminAuthorization(request);
        request.Content = JsonContent.Create(new
        {
            displayName = "测试调用",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = DefaultScopes
        });
        var response = await client.SendAsync(request);
        var responseBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, responseBody);
        return (JsonSerializer.Deserialize<ApiKeyCreatedResponse>(responseBody, JsonOptions))!;
    }

    private static void AddAdminAuthorization(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
    }
}
