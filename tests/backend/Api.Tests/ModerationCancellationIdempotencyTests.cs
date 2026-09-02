using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;

namespace VeriScan.Api.Tests;

/// <summary>取消异步审核批次的幂等、校验和原子事实测试。</summary>
public sealed class ModerationCancellationIdempotencyTests
{
    private static readonly string[] ModerationScopes = ["moderation:submit", "moderation:read"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task CancelRequiresExactlyOneValidIdempotencyKey()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "取消请求头校验");
        var apiKey = await CreateApiKeyAsync(client, application.Id);
        var submitted = await SubmitAsync(client, apiKey, "header-validation-item");

        using var missing = CreateCancelRequest(submitted.RequestId, apiKey);
        var missingResponse = await client.SendAsync(missing);
        Assert.Equal(HttpStatusCode.BadRequest, missingResponse.StatusCode);

        using var invalid = CreateCancelRequest(submitted.RequestId, apiKey);
        invalid.Headers.Add("Idempotency-Key", "too-short");
        var invalidResponse = await client.SendAsync(invalid);
        Assert.Equal(HttpStatusCode.BadRequest, invalidResponse.StatusCode);

        using var duplicate = CreateCancelRequest(submitted.RequestId, apiKey);
        Assert.True(duplicate.Headers.TryAddWithoutValidation(
            "Idempotency-Key",
            ["cancel-header-20260902-a", "cancel-header-20260902-a"]));
        var duplicateResponse = await client.SendAsync(duplicate);
        Assert.Equal(HttpStatusCode.BadRequest, duplicateResponse.StatusCode);

        using var valid = CreateCancelRequest(submitted.RequestId, apiKey);
        valid.Headers.Add("Idempotency-Key", "cancel-header-valid-20260902");
        var validResponse = await client.SendAsync(valid);
        Assert.Equal(HttpStatusCode.OK, validResponse.StatusCode);
    }

    [Fact]
    public async Task SameCancelKeyReplaysExactResponseWithoutDuplicatingOutbox()
    {
        await using var factory = await CreateFactoryAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "取消幂等原子性");
        var apiKey = await CreateApiKeyAsync(client, application.Id);
        const string sharedKey = "shared-cancel-key-20260902";
        var submitted = await SubmitAsync(client, apiKey, "cancel-idempotent-item", sharedKey);

        using var firstRequest = CreateCancelRequest(submitted.RequestId, apiKey);
        firstRequest.Headers.Add("Idempotency-Key", sharedKey);
        var firstResponse = await client.SendAsync(firstRequest);
        var firstBody = await firstResponse.Content.ReadAsStringAsync();

        using var replayRequest = CreateCancelRequest(submitted.RequestId, apiKey);
        replayRequest.Headers.Add("Idempotency-Key", sharedKey);
        var replayResponse = await client.SendAsync(replayRequest);
        var replayBody = await replayResponse.Content.ReadAsStringAsync();

        Assert.Equal(HttpStatusCode.OK, firstResponse.StatusCode);
        Assert.Equal(HttpStatusCode.OK, replayResponse.StatusCode);
        Assert.Equal(firstBody, replayBody);

        using var differentKeyRequest = CreateCancelRequest(submitted.RequestId, apiKey);
        differentKeyRequest.Headers.Add("Idempotency-Key", "cancel-different-key-20260902");
        var differentKeyResponse = await client.SendAsync(differentKeyRequest);
        Assert.Equal(HttpStatusCode.Conflict, differentKeyResponse.StatusCode);
        Assert.Equal(
            "request_conflict",
            await ReadProblemCodeAsync(differentKeyResponse));

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
        Assert.Equal(
            1,
            await dbContext.IdempotentOperations
                .AsNoTracking()
                .CountAsync(operation =>
                    operation.ApplicationId == application.Id &&
                    operation.TargetRequestId == submitted.RequestId &&
                    operation.Operation == "cancel"));
        Assert.Equal(
            1,
            await dbContext.OutboxEvents
                .AsNoTracking()
                .CountAsync(outboxEvent =>
                    outboxEvent.ApplicationId == application.Id &&
                    outboxEvent.AggregateId == submitted.RequestId &&
                    outboxEvent.EventType == "moderation.cancelled"));
        var requestEvents = await dbContext.ApiRequestEvents
            .AsNoTracking()
            .Where(requestEvent =>
                requestEvent.ApplicationId == application.Id &&
                requestEvent.ModerationRequestId == submitted.RequestId &&
                requestEvent.RouteTemplate == "/api/v1/moderation/batches/{requestId}/cancel")
            .ToArrayAsync();
        Assert.Equal(3, requestEvents.Length);
        Assert.Contains(requestEvents, item => item.IdempotencyOutcome == "new_idempotent");
        Assert.Contains(requestEvents, item => item.IdempotencyOutcome == "replay");
        Assert.Contains(requestEvents, item => item.IdempotencyOutcome == "conflict");

        var job = await dbContext.ModerationJobs
            .AsNoTracking()
            .SingleAsync(item => item.RequestId == submitted.RequestId);
        var request = await dbContext.ModerationRequests
            .AsNoTracking()
            .Include(item => item.Items)
            .SingleAsync(item => item.Id == submitted.RequestId);
        Assert.Equal(ModerationJobStatus.Cancelled, job.Status);
        Assert.Equal(ModerationProcessingStatus.Cancelled, request.ProcessingStatus);
        Assert.All(request.Items, item =>
            Assert.Equal(ModerationProcessingStatus.Cancelled, item.ProcessingStatus));

        var operation = await dbContext.IdempotentOperations
            .AsNoTracking()
            .SingleAsync(item => item.TargetRequestId == submitted.RequestId);
        Assert.Equal(TimeSpan.FromHours(24), operation.ExpiresAt - operation.CreatedAt);
        Assert.Equal(JsonDocument.Parse(firstBody).RootElement.GetRawText(), operation.ResponseSnapshot);
    }

    private static async Task<ApiTestFactory> CreateFactoryAsync()
    {
        var factory = new ApiTestFactory(services =>
        {
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
        });
        await factory.SeedRulesAsync();
        return factory;
    }

    private static HttpRequestMessage CreateCancelRequest(Guid requestId, string apiKey)
    {
        var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/moderation/batches/{requestId}/cancel");
        request.Headers.Add("X-API-Key", apiKey);
        return request;
    }

    private static async Task<BatchModerationResponse> SubmitAsync(
        HttpClient client,
        string apiKey,
        string itemId,
        string? idempotencyKey = null)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        if (idempotencyKey is not null)
        {
            request.Headers.Add("Idempotency-Key", idempotencyKey);
        }

        request.Content = JsonContent.Create(new
        {
            mode = "async",
            items = new[]
            {
                new
                {
                    id = itemId,
                    content = "等待取消的异步审核内容",
                    contentType = "plain_text"
                }
            }
        });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.StatusCode == HttpStatusCode.Accepted, body);
        return JsonSerializer.Deserialize<BatchModerationResponse>(body, JsonOptions)!;
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(
        HttpClient client,
        string name)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/admin/v1/applications");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        request.Content = JsonContent.Create(new { name, environment = "test" });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ApplicationResponse>(body, JsonOptions)!;
    }

    private static async Task<string> CreateApiKeyAsync(HttpClient client, Guid applicationId)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        request.Content = JsonContent.Create(new
        {
            displayName = "取消幂等测试凭证",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = ModerationScopes
        });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ApiKeyCreatedResponse>(body, JsonOptions)!.ApiKey;
    }

    private static async Task<string?> ReadProblemCodeAsync(HttpResponseMessage response)
    {
        var body = await response.Content.ReadAsStringAsync();
        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("code", out var code)
            ? code.GetString()
            : null;
    }
}
