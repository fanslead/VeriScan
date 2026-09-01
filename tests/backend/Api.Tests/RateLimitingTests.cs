using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;

namespace VeriScan.Api.Tests;

public sealed class RateLimitingTests
{
    private static readonly string[] DefaultScopes = ["moderation:submit", "moderation:read"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task ApiKeyLimitReturnsRateLimitHeadersAndProblemDetails()
    {
        await using var baseFactory = new ApiTestFactory();
        await baseFactory.SeedRulesAsync();
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:ApiKeyPermitLimit"] = "1",
                    ["RateLimiting:ApiKeyWindowSeconds"] = "60",
                    ["RateLimiting:ApiKeyQueueLimit"] = "0",
                    ["RateLimiting:ApiKeyConcurrencyLimit"] = "1",
                    ["RateLimiting:ApiKeyConcurrencyQueueLimit"] = "0"
                })));
        using var client = factory.CreateClient();

        var application = await CreateApplicationAsync(client);
        var apiKey = await CreateApiKeyAsync(client, application.Id);

        using var first = CreateModerationRequest(apiKey);
        using var second = CreateModerationRequest(apiKey);
        var firstResponse = await client.SendAsync(first);
        var secondResponse = await client.SendAsync(second);

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
        Assert.Equal("1", secondResponse.Headers.GetValues("RateLimit-Limit").Single());
        Assert.Equal("0", secondResponse.Headers.GetValues("RateLimit-Remaining").Single());
        Assert.True(int.Parse(
            secondResponse.Headers.GetValues("Retry-After").Single(),
            CultureInfo.InvariantCulture) > 0);
        Assert.Equal("application/problem+json", secondResponse.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal(
            "rate_limit_exceeded",
            body.RootElement.GetProperty("code").GetString());
        Assert.Equal("api_key", body.RootElement.GetProperty("scope").GetString());
    }

    [Fact]
    public async Task GlobalConcurrencyLimitRejectsExcessWorkBeforeItReachesTheService()
    {
        await using var baseFactory = new ApiTestFactory();
        await baseFactory.SeedRulesAsync();
        await using var factory = baseFactory.WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, configuration) =>
                configuration.AddInMemoryCollection(new Dictionary<string, string?>
                {
                    ["RateLimiting:GlobalPermitLimit"] = "100",
                    ["RateLimiting:GlobalConcurrencyLimit"] = "1",
                    ["RateLimiting:ApplicationConcurrencyLimit"] = "10",
                    ["RateLimiting:ApplicationConcurrencyQueueLimit"] = "0",
                    ["RateLimiting:ApiKeyConcurrencyLimit"] = "10",
                    ["RateLimiting:ApiKeyConcurrencyQueueLimit"] = "0"
                })));
        using var client = factory.CreateClient();
        var aiClient = factory.Services.GetRequiredService<IModerationAiClient>() as TestModerationAiClient;
        Assert.NotNull(aiClient);
        aiClient.Delay = TimeSpan.FromMilliseconds(500);

        var application = await CreateApplicationAsync(client);
        var apiKey = await CreateApiKeyAsync(client, application.Id);
        using var first = CreateModerationRequest(apiKey);
        using var second = CreateModerationRequest(apiKey);

        var firstTask = client.SendAsync(first);
        await Task.Delay(100);
        var secondResponse = await client.SendAsync(second);
        var firstResponse = await firstTask;

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.TooManyRequests, secondResponse.StatusCode);
        using var body = JsonDocument.Parse(await secondResponse.Content.ReadAsStringAsync());
        Assert.Equal("rate_limit_exceeded", body.RootElement.GetProperty("code").GetString());
    }

    private static HttpRequestMessage CreateModerationRequest(string apiKey)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode = "sync",
            items = new[]
            {
                new { id = "one", content = "普通文本", contentType = "plain_text" }
            }
        });
        return request;
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(HttpClient client)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/applications")
        {
            Content = JsonContent.Create(new { name = "限流测试", environment = "test" })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;
    }

    private static async Task<string> CreateApiKeyAsync(HttpClient client, Guid applicationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys")
        {
            Content = JsonContent.Create(new
            {
                displayName = "限流测试",
                expiresAt = DateTimeOffset.UtcNow.AddHours(1),
                scopes = DefaultScopes
            })
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions))!.ApiKey;
    }
}
