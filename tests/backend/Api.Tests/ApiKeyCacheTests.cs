using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Caching.Distributed;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;
using VeriScan.Infrastructure.Persistence.Repositories;

namespace VeriScan.Api.Tests;

public sealed class ApiKeyCacheTests
{
    private static readonly string[] DefaultScopes = ["moderation:submit", "moderation:read"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task ReusesCachedIdentityAndInvalidatesItAfterRevocation()
    {
        var counter = new VerificationLookupCounter();
        await using var factory = new ApiTestFactory(services =>
        {
            services.RemoveAll<IApiKeyStore>();
            services.AddScoped<ApiKeyStore>();
            services.AddScoped<IApiKeyStore>(serviceProvider => new CountingApiKeyStore(
                serviceProvider.GetRequiredService<ApiKeyStore>(),
                counter));
        });
        await factory.SeedRulesAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client);
        var key = await CreateApiKeyAsync(client, application.Id);

        using var first = CreateModerationRequest(key.ApiKey, "cache-first");
        using var second = CreateModerationRequest(key.ApiKey, "cache-second");
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(first)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.SendAsync(second)).StatusCode);
        Assert.Equal(1, counter.Count);

        using var revoke = CreateAdminRequest(
            HttpMethod.Delete,
            $"/api/admin/v1/applications/{application.Id}/api-keys/{key.KeyId}");
        Assert.Equal(HttpStatusCode.NoContent, (await client.SendAsync(revoke)).StatusCode);

        using var revoked = CreateModerationRequest(key.ApiKey, "cache-after-revoke");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.SendAsync(revoked)).StatusCode);
        Assert.Equal(2, counter.Count);
    }

    [Fact]
    public async Task DistributedCacheFailureFallsBackToDatabaseAuthentication()
    {
        await using var factory = new ApiTestFactory(services =>
        {
            services.RemoveAll<IDistributedCache>();
            services.AddSingleton<IDistributedCache, FailingDistributedCache>();
        });
        await factory.SeedRulesAsync();
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client);
        var key = await CreateApiKeyAsync(client, application.Id);

        using var request = CreateModerationRequest(key.ApiKey, "cache-fallback");
        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage CreateModerationRequest(string apiKey, string id)
    {
        var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode = "sync",
            items = new[] { new { id, content = "明鉴内容服务", contentType = "plain_text" } }
        });
        return request;
    }

    private static async Task<ApplicationResponse> CreateApplicationAsync(HttpClient client)
    {
        using var request = CreateAdminRequest(HttpMethod.Post, "/api/admin/v1/applications");
        request.Content = JsonContent.Create(new { name = "缓存验证应用", environment = "test" });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApplicationResponse>(JsonOptions))!;
    }

    private static async Task<ApiKeyCreatedResponse> CreateApiKeyAsync(
        HttpClient client,
        Guid applicationId)
    {
        using var request = CreateAdminRequest(
            HttpMethod.Post,
            $"/api/admin/v1/applications/{applicationId}/api-keys");
        request.Content = JsonContent.Create(new
        {
            displayName = "缓存验证凭证",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = DefaultScopes
        });
        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<ApiKeyCreatedResponse>(JsonOptions))!;
    }

    private static HttpRequestMessage CreateAdminRequest(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", "test-admin");
        return request;
    }

    private sealed class VerificationLookupCounter
    {
        private int count;

        public int Count => Volatile.Read(ref count);

        public void Increment() => Interlocked.Increment(ref count);
    }

    private sealed class CountingApiKeyStore(
        ApiKeyStore inner,
        VerificationLookupCounter counter) : IApiKeyStore
    {
        public Task AddAsync(ApplicationApiKey apiKey, CancellationToken cancellationToken) =>
            inner.AddAsync(apiKey, cancellationToken);

        public Task<ApplicationApiKey?> GetByIdAsync(
            Guid applicationId,
            Guid keyId,
            CancellationToken cancellationToken) =>
            inner.GetByIdAsync(applicationId, keyId, cancellationToken);

        public Task<ApiKeyVerificationData?> GetVerificationDataAsync(
            string publicKeyId,
            CancellationToken cancellationToken)
        {
            counter.Increment();
            return inner.GetVerificationDataAsync(publicKeyId, cancellationToken);
        }

        public Task<IReadOnlyList<ApplicationApiKey>> ListByApplicationAsync(
            Guid applicationId,
            CancellationToken cancellationToken) =>
            inner.ListByApplicationAsync(applicationId, cancellationToken);

        public Task SaveChangesAsync(CancellationToken cancellationToken) =>
            inner.SaveChangesAsync(cancellationToken);
    }

    private sealed class FailingDistributedCache : IDistributedCache
    {
        public byte[]? Get(string key) => throw CacheFailure();

        public Task<byte[]?> GetAsync(string key, CancellationToken token = default) =>
            Task.FromException<byte[]?>(CacheFailure());

        public void Refresh(string key) => throw CacheFailure();

        public Task RefreshAsync(string key, CancellationToken token = default) =>
            Task.FromException(CacheFailure());

        public void Remove(string key) => throw CacheFailure();

        public Task RemoveAsync(string key, CancellationToken token = default) =>
            Task.FromException(CacheFailure());

        public void Set(string key, byte[] value, DistributedCacheEntryOptions options) =>
            throw CacheFailure();

        public Task SetAsync(
            string key,
            byte[] value,
            DistributedCacheEntryOptions options,
            CancellationToken token = default) =>
            Task.FromException(CacheFailure());

        private static InvalidOperationException CacheFailure() =>
            new("synthetic distributed cache outage");
    }
}
