using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Tests;

public sealed class AdminReadApiTests : IClassFixture<ApiTestFactory>
{
    private static readonly string[] DefaultScopes = ["moderation:submit", "moderation:read"];
    private static readonly string[] ReviewPreviews = ["请加微信联系", "普通内容"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };
    private readonly ApiTestFactory factory;

    public AdminReadApiTests(ApiTestFactory factory)
    {
        this.factory = factory;
        factory.SeedRulesAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task AdminReadEndpointsRequireAdminBearer()
    {
        using var client = factory.CreateClient();
        var paths = new[]
        {
            "/api/admin/v1/overview",
            "/api/admin/v1/moderation-records",
            $"/api/admin/v1/moderation-records/{Guid.CreateVersion7()}"
        };

        foreach (var path in paths)
        {
            var response = await client.GetAsync(path);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        }
    }

    [Fact]
    public async Task ModerationRecordsAreFilteredMappedAndPagedFromStoredFacts()
    {
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "记录查询应用");
        var key = await CreateApiKeyAsync(client, application.Id, "查询凭证");
        var batch = await SubmitBatchAsync(client, key.ApiKey, new[]
        {
            new { id = "reject-1", content = "这是赌博内容", contentType = "plain_text" },
            new { id = "review-1", content = "请加微信联系", contentType = "plain_text" },
            new { id = "review-2", content = "普通内容", contentType = "plain_text" }
        });

        using var listRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/v1/moderation-records?applicationId={application.Id}&status=review&page=1&pageSize=1");
        AddAdminAuthorization(listRequest);
        var listResponse = await client.SendAsync(listRequest);
        var listBody = await listResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var page = JsonSerializer.Deserialize<ModerationRecordPageResponse>(listBody, JsonOptions);
        Assert.NotNull(page);
        Assert.Equal(2, page.Total);
        Assert.Single(page.Items);
        Assert.Equal(1, page.Page);
        Assert.Equal(1, page.PageSize);

        var record = page.Items[0];
        Assert.Equal(application.Id, record.ApplicationId);
        Assert.Equal("记录查询应用", record.ApplicationName);
        Assert.Equal(ModerationDecision.Review, record.Decision);
        Assert.Equal(1, record.DetectLevel);
        Assert.StartsWith("ruleset@", record.PolicyVersion, StringComparison.Ordinal);
        Assert.NotEmpty(record.ContentHash);
        Assert.NotEmpty(record.ReasonCodes);
        Assert.Empty(record.Evidence);
        Assert.Contains(record.ContentPreview, ReviewPreviews);

        using var detailRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/v1/moderation-records/{record.Id}");
        AddAdminAuthorization(detailRequest);
        var detailResponse = await client.SendAsync(detailRequest);
        Assert.Equal(HttpStatusCode.OK, detailResponse.StatusCode);
        var detail = await detailResponse.Content.ReadFromJsonAsync<ModerationRecordResponse>(JsonOptions);
        Assert.NotNull(detail);
        Assert.Equal(record.Id, detail.Id);
        Assert.Equal(record.ContentHash, detail.ContentHash);

        using var keywordRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/v1/moderation-records?applicationId={application.Id}&keyword=%E8%B5%8C%E5%8D%9A&pageSize=10");
        AddAdminAuthorization(keywordRequest);
        var keywordResponse = await client.SendAsync(keywordRequest);
        Assert.Equal(HttpStatusCode.OK, keywordResponse.StatusCode);
        var keywordPage = await keywordResponse.Content.ReadFromJsonAsync<ModerationRecordPageResponse>(JsonOptions);
        Assert.NotNull(keywordPage);
        Assert.Single(keywordPage.Items);
        Assert.Equal(ModerationDecision.Reject, keywordPage.Items[0].Decision);

        using var invalidPageRequest = new HttpRequestMessage(
            HttpMethod.Get,
            "/api/admin/v1/moderation-records?page=0&pageSize=101");
        AddAdminAuthorization(invalidPageRequest);
        var invalidPageResponse = await client.SendAsync(invalidPageRequest);
        Assert.Equal(HttpStatusCode.BadRequest, invalidPageResponse.StatusCode);

        _ = batch;
    }

    [Fact]
    public async Task OverviewUsesCurrentDayFactsAndComputesLatencyFromCompletedItems()
    {
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "概览应用");
        var key = await CreateApiKeyAsync(client, application.Id, "概览凭证");
        await SubmitBatchAsync(client, key.ApiKey, new[]
        {
            new { id = "overview-reject", content = "这是赌博内容", contentType = "plain_text" },
            new { id = "overview-review", content = "请加微信联系", contentType = "plain_text" }
        });

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/admin/v1/overview");
        AddAdminAuthorization(request);
        var response = await client.SendAsync(request);
        var overviewBody = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.OK, overviewBody);
        var overview = JsonSerializer.Deserialize<AdminOverviewResponse>(overviewBody, JsonOptions);
        Assert.NotNull(overview);
        Assert.True(overview.TodayRequests >= 1);
        Assert.True(overview.TodayItems >= 2);
        Assert.True(overview.RejectCount >= 1);
        Assert.True(overview.ReviewCount >= 1);
        Assert.NotNull(overview.RejectRate);
        Assert.NotNull(overview.ReviewRate);
        Assert.NotNull(overview.P95LatencyMs);
        Assert.True(overview.P95LatencyMs >= 0);
        Assert.NotEmpty(overview.Trend);
        Assert.NotEmpty(overview.RecentRecords);
        Assert.Null(overview.RequestDelta);
        Assert.Null(overview.RejectDelta);
        Assert.Null(overview.ReviewDelta);
        Assert.Null(overview.LatencyDelta);
    }

    [Fact]
    public async Task ApiKeyDisplayNameRoundTripsThroughCreateListAndRotate()
    {
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "凭证命名应用");
        var key = await CreateApiKeyAsync(client, application.Id, "生产服务");
        Assert.Equal("生产服务", key.DisplayName);

        using var listRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/v1/applications/{application.Id}/api-keys");
        AddAdminAuthorization(listRequest);
        var listResponse = await client.SendAsync(listRequest);
        Assert.Equal(HttpStatusCode.OK, listResponse.StatusCode);
        var list = await listResponse.Content.ReadFromJsonAsync<ApiKeyListResponse>(JsonOptions);
        Assert.NotNull(list);
        Assert.Contains(list.Items, item => item.KeyId == key.KeyId && item.DisplayName == "生产服务");

        using var rotateRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{application.Id}/api-keys/{key.KeyId}/rotate");
        AddAdminAuthorization(rotateRequest);
        rotateRequest.Content = JsonContent.Create(new
        {
            displayName = "生产服务轮换",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            revokeOldKey = false,
            scopes = DefaultScopes
        });
        var rotateResponse = await client.SendAsync(rotateRequest);
        var rotatedBody = await rotateResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Created, rotateResponse.StatusCode);
        var rotated = JsonSerializer.Deserialize<ApiKeyCreatedResponse>(rotatedBody, JsonOptions);
        Assert.NotNull(rotated);
        Assert.Equal("生产服务轮换", rotated.DisplayName);
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

    private static async Task<BatchModerationResponse> SubmitBatchAsync(
        HttpClient client,
        string apiKey,
        object[] items)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new { mode = "sync", items });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<BatchModerationResponse>(JsonOptions))!;
    }

    private static void AddAdminAuthorization(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
    }
}
