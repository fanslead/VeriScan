using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Tests;

public sealed class RuleSetApiTests : IClassFixture<ApiTestFactory>
{
    private static readonly string[] ModerationScopes = ["moderation:submit", "moderation:read"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    private readonly ApiTestFactory factory;

    public RuleSetApiTests(ApiTestFactory factory)
    {
        this.factory = factory;
        factory.SeedRulesAsync().GetAwaiter().GetResult();
    }

    [Fact]
    public async Task DraftMustValidateBeforePublishAndPublishedVersionIsImmutable()
    {
        using var client = factory.CreateClient();
        var draft = await CreateRuleSetAsync(client, "营销风险规则", [
            new("站外交易", "black", "commerce", 1m),
            new("加群", "suspicious", "contact", 0.6m)
        ]);

        using var updateDraft = AdminRequest(HttpMethod.Put, $"/api/admin/v1/rule-sets/{draft.Id}");
        updateDraft.Content = JsonContent.Create(new
        {
            name = "营销与导流规则",
            rules = new[]
            {
                new { term = "站外交易", type = "black", category = "commerce", weight = 1m },
                new { term = "加群领取", type = "suspicious", category = "contact", weight = 0.7m }
            }
        });
        var updateResponse = await client.SendAsync(updateDraft);
        updateResponse.EnsureSuccessStatusCode();
        var updated = await updateResponse.Content.ReadFromJsonAsync<RuleSetResponse>(JsonOptions);
        Assert.Equal("营销与导流规则", updated!.Name);
        Assert.Equal(2, updated.RuleCount);
        Assert.Null(updated.LastValidatedAt);

        using var prematurePublish = AdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/rule-sets/{draft.Id}/publish");
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(prematurePublish)).StatusCode);

        using var validate = AdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/rule-sets/{draft.Id}/validate");
        var validationResponse = await client.SendAsync(validate);
        validationResponse.EnsureSuccessStatusCode();
        var validation = await validationResponse.Content.ReadFromJsonAsync<RuleSetValidationResponse>(JsonOptions);
        Assert.NotNull(validation);
        Assert.True(validation.Valid);
        Assert.Equal(2, validation.RuleCount);
        Assert.Equal(64, validation.Checksum.Length);

        using var publish = AdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/rule-sets/{draft.Id}/publish");
        var publishResponse = await client.SendAsync(publish);
        publishResponse.EnsureSuccessStatusCode();
        var published = await publishResponse.Content.ReadFromJsonAsync<RuleSetResponse>(JsonOptions);
        Assert.Equal(RuleSetStatus.Published, published!.Status);
        Assert.Equal(validation.Checksum, published.PublishedChecksum);

        using var overwrite = AdminRequest(HttpMethod.Put, $"/api/admin/v1/rule-sets/{draft.Id}");
        overwrite.Content = JsonContent.Create(new
        {
            name = "不应覆盖",
            rules = new[] { new { term = "x", type = "black", category = "test", weight = 1m } }
        });
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(overwrite)).StatusCode);

        using var clone = AdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/rule-sets/{draft.Id}/revisions");
        var cloneResponse = await client.SendAsync(clone);
        cloneResponse.EnsureSuccessStatusCode();
        var revision = await cloneResponse.Content.ReadFromJsonAsync<RuleSetResponse>(JsonOptions);
        Assert.Equal(RuleSetStatus.Draft, revision!.Status);
        Assert.NotEqual(published.PublicRevisionId, revision.PublicRevisionId);
        Assert.Equal(published.RuleCount, revision.RuleCount);
    }

    [Fact]
    public async Task ApplicationBindingControlsRequestedPolicyAndPersistsRevision()
    {
        using var client = factory.CreateClient();
        var draft = await CreateRuleSetAsync(client, "应用专用规则", [
            new("专用禁词", "black", "tenant_policy", 1m)
        ]);
        await ValidateAndPublishAsync(client, draft.Id);
        var application = await CreateApplicationAsync(client);

        using var bind = AdminRequest(
            HttpMethod.Put,
            $"/api/admin/v1/applications/{application.Id}/rule-set");
        bind.Content = JsonContent.Create(new { publicRevisionId = draft.PublicRevisionId });
        var bindResponse = await client.SendAsync(bind);
        bindResponse.EnsureSuccessStatusCode();
        var bound = await bindResponse.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions);
        Assert.Equal(draft.PublicRevisionId, bound!.RuleSetRevisionId);
        Assert.Equal("应用专用规则", bound.RuleSetName);

        var key = await CreateApiKeyAsync(client, application.Id);
        using var mismatch = ModerationRequest(key.ApiKey, "ruleset@not-bound", "专用禁词");
        Assert.Equal(HttpStatusCode.Conflict, (await client.SendAsync(mismatch)).StatusCode);

        using var moderate = ModerationRequest(key.ApiKey, draft.PublicRevisionId, "专用禁词");
        var moderationResponse = await client.SendAsync(moderate);
        moderationResponse.EnsureSuccessStatusCode();
        var result = await moderationResponse.Content.ReadFromJsonAsync<BatchModerationResponse>(JsonOptions);
        Assert.Equal(draft.PublicRevisionId, result!.PolicyId);
        Assert.Equal(ModerationDecision.Reject, Assert.Single(result.Results).Decision);

        using var records = AdminRequest(HttpMethod.Get, "/api/admin/v1/moderation-records?page=1&pageSize=20");
        var recordsResponse = await client.SendAsync(records);
        recordsResponse.EnsureSuccessStatusCode();
        var page = await recordsResponse.Content.ReadFromJsonAsync<ModerationRecordPageResponse>(JsonOptions);
        Assert.Contains(page!.Items, record =>
            record.ApplicationId == application.Id && record.PolicyVersion == draft.PublicRevisionId);
    }

    [Fact]
    public async Task ConflictingNormalizedRulesFailValidation()
    {
        using var client = factory.CreateClient();
        var draft = await CreateRuleSetAsync(client, "冲突检查", [
            new("ＡＢＣ", "black", "spam", 1m),
            new("abc", "white", "spam", 0.1m)
        ]);

        using var validate = AdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/rule-sets/{draft.Id}/validate");
        var response = await client.SendAsync(validate);
        response.EnsureSuccessStatusCode();
        var validation = await response.Content.ReadFromJsonAsync<RuleSetValidationResponse>(JsonOptions);
        Assert.False(validation!.Valid);
        Assert.Contains(validation.Issues, issue => issue.Code == "CONFLICTING_RULE");
    }

    private static async Task<RuleSetResponse> CreateRuleSetAsync(
        HttpClient client,
        string name,
        IReadOnlyList<TestRule> rules)
    {
        using var request = AdminRequest(HttpMethod.Post, "/api/admin/v1/rule-sets");
        request.Content = JsonContent.Create(new
        {
            name,
            rules = rules.Select(rule => new
            {
                term = rule.Term,
                type = rule.Type,
                category = rule.Category,
                weight = rule.Weight
            })
        });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<RuleSetResponse>(body, JsonOptions)!;
    }

    private static async Task ValidateAndPublishAsync(HttpClient client, Guid ruleSetId)
    {
        using var validate = AdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/rule-sets/{ruleSetId}/validate");
        (await client.SendAsync(validate)).EnsureSuccessStatusCode();
        using var publish = AdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/rule-sets/{ruleSetId}/publish");
        (await client.SendAsync(publish)).EnsureSuccessStatusCode();
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(HttpClient client)
    {
        using var request = AdminRequest(HttpMethod.Post, "/api/admin/v1/applications");
        request.Content = JsonContent.Create(new { name = "策略绑定应用", environment = "test" });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;
    }

    private static async Task<ApiKeyCreatedResponse> CreateApiKeyAsync(HttpClient client, Guid applicationId)
    {
        using var request = AdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        request.Content = JsonContent.Create(new
        {
            displayName = "规则测试",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = ModerationScopes
        });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions))!;
    }

    private static HttpRequestMessage ModerationRequest(string apiKey, string policyId, string content)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            policyId,
            mode = "sync",
            items = new[] { new { id = "rule-item", content, contentType = "plain_text" } }
        });
        return request;
    }

    private static HttpRequestMessage AdminRequest(HttpMethod method, string uri)
    {
        var request = new HttpRequestMessage(method, uri);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        return request;
    }

    private sealed record TestRule(string Term, string Type, string Category, decimal Weight);
}
