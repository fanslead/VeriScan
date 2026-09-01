using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;

namespace VeriScan.Api.Tests;

public sealed class OperationalFactsUsageTests
{
    private static readonly string[] DefaultScopes = ["moderation:submit", "moderation:read"];
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task ManagementWritesPersistSafeAuditAndOutboxFacts()
    {
        await using var factory = new ApiTestFactory();
        await factory.SeedRulesAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "审计事实应用");
        var key = await CreateApiKeyAsync(client, application.Id);

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
        var auditEvents = await dbContext.AuditEvents.AsNoTracking().ToArrayAsync();
        var outboxEvents = await dbContext.OutboxEvents.AsNoTracking().ToArrayAsync();

        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.Action == "application.created" &&
            auditEvent.ResourceId == application.Id.ToString());
        Assert.Contains(auditEvents, auditEvent =>
            auditEvent.Action == "api_key.created" &&
            auditEvent.ApiKeyId == key.KeyId);
        Assert.All(auditEvents, auditEvent =>
        {
            Assert.DoesNotContain(key.ApiKey, auditEvent.BeforeJson ?? "", StringComparison.Ordinal);
            Assert.DoesNotContain(key.ApiKey, auditEvent.AfterJson ?? "", StringComparison.Ordinal);
        });
        Assert.Contains(outboxEvents, outboxEvent =>
            outboxEvent.EventType == "application.created" &&
            outboxEvent.ApplicationId == application.Id);
        Assert.Contains(outboxEvents, outboxEvent =>
            outboxEvent.EventType == "api_key.created" &&
            outboxEvent.AggregateId == key.KeyId);
        Assert.All(outboxEvents, outboxEvent =>
        {
            Assert.DoesNotContain(key.ApiKey, outboxEvent.PayloadJson, StringComparison.Ordinal);
        });

        using var auditRequest = new HttpRequestMessage(
            HttpMethod.Get,
            $"/api/admin/v1/audit-events?applicationId={application.Id}");
        AddAdminAuthorization(auditRequest);
        var auditResponse = await client.SendAsync(auditRequest);
        var auditBody = await auditResponse.Content.ReadAsStringAsync();
        Assert.True(auditResponse.StatusCode == HttpStatusCode.OK, auditBody);
        Assert.Contains("application.created", auditBody, StringComparison.Ordinal);
        Assert.DoesNotContain(key.ApiKey, auditBody, StringComparison.Ordinal);
    }

    [Fact]
    public async Task IdempotentReplayDoesNotDuplicateAiFactsOrRebuildUsage()
    {
        await using var factory = new ApiTestFactory();
        await factory.SeedRulesAsync();
        var aiClient = Assert.IsType<TestModerationAiClient>(
            factory.Services.GetRequiredService<IModerationAiClient>());
        aiClient.Result = new AiModerationResult(
            AiModerationOutcome.Succeeded,
            AiModerationLabel.Safe,
            ["MODEL_SAFE"],
            [],
            [],
            "usage-test@1",
            "provider-1",
            12,
            8,
            null);

        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "幂等计量应用");
        var key = await CreateApiKeyAsync(client, application.Id);
        var from = DateTimeOffset.UtcNow.AddMinutes(-2);
        var first = await SubmitBatchAsync(client, key.ApiKey, "idem-usage-key-1");
        var second = await SubmitBatchAsync(client, key.ApiKey, "idem-usage-key-1");
        Assert.Equal(first.RequestId, second.RequestId);

        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
            Assert.Equal(
                1,
                await dbContext.ModerationRequests
                    .AsNoTracking()
                    .CountAsync(request => request.ApplicationId == application.Id));
            Assert.Equal(
                1,
                await dbContext.AiInvocations
                    .AsNoTracking()
                    .CountAsync(invocation => invocation.ApplicationId == application.Id));
            var requestEvents = await dbContext.ApiRequestEvents
                .AsNoTracking()
                .Where(requestEvent => requestEvent.ApplicationId == application.Id)
                .ToArrayAsync();
            Assert.Equal(2, requestEvents.Length);
            Assert.Contains(requestEvents, requestEvent => requestEvent.IdempotencyOutcome == "new_idempotent");
            Assert.Contains(requestEvents, requestEvent => requestEvent.IdempotencyOutcome == "replay");
            Assert.Single(dbContext.OutboxEvents.AsNoTracking().Where(
                outboxEvent => outboxEvent.EventType == "moderation.completed"));
            var moderationEvent = await dbContext.OutboxEvents
                .AsNoTracking()
                .SingleAsync(outboxEvent => outboxEvent.EventType == "moderation.completed");
            Assert.DoesNotContain("请加微信联系", moderationEvent.PayloadJson, StringComparison.Ordinal);
        }

        var through = DateTimeOffset.UtcNow.AddMinutes(2);
        using var rebuildRequest = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{application.Id}/usage/rebuild" +
            $"?from={Uri.EscapeDataString(from.ToString("O"))}" +
            $"&through={Uri.EscapeDataString(through.ToString("O"))}");
        AddAdminAuthorization(rebuildRequest);
        var rebuildResponse = await client.SendAsync(rebuildRequest);
        var rebuildBody = await rebuildResponse.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, rebuildResponse.StatusCode);
        var rebuild = JsonSerializer.Deserialize<UsageRebuildResponse>(rebuildBody, JsonOptions);
        Assert.NotNull(rebuild);
        Assert.Equal(1, rebuild.RequestCount);
        Assert.Equal(1, rebuild.ItemCount);
        Assert.Equal(1, rebuild.AiCallCount);

        await using var finalScope = factory.Services.CreateAsyncScope();
        var finalDbContext = finalScope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
        var hourly = await finalDbContext.UsageHourly
            .AsNoTracking()
            .SingleAsync(usage => usage.ApplicationId == application.Id && usage.ApiKeyId == key.KeyId);
        var daily = await finalDbContext.UsageDaily
            .AsNoTracking()
            .SingleAsync(usage => usage.ApplicationId == application.Id && usage.ApiKeyId == key.KeyId);
        Assert.Equal(1, hourly.RequestCount);
        Assert.Equal(1, hourly.IdempotencyReplayCount);
        Assert.Equal(1, hourly.ItemCount);
        Assert.Equal(1, hourly.AiCallCount);
        Assert.Equal(12, hourly.InputTokens);
        Assert.Equal(8, hourly.OutputTokens);
        Assert.Equal(hourly.RequestCount, daily.RequestCount);
        Assert.Equal(hourly.AiCallCount, daily.AiCallCount);
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
        Guid applicationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        AddAdminAuthorization(request);
        request.Content = JsonContent.Create(new
        {
            displayName = "计量测试凭证",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = DefaultScopes
        });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ApiKeyCreatedResponse>(body, JsonOptions)!;
    }

    private static async Task<BatchModerationResponse> SubmitBatchAsync(
        HttpClient client,
        string apiKey,
        string idempotencyKey)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        request.Content = JsonContent.Create(new
        {
            mode = "sync",
            items = new[]
            {
                new { id = "item-1", content = "请加微信联系", contentType = "plain_text" }
            }
        });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<BatchModerationResponse>(body, JsonOptions)!;
    }

    private static void AddAdminAuthorization(HttpRequestMessage request)
    {
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
    }
}
