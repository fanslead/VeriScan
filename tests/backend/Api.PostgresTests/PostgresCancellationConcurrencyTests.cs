using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Testcontainers.PostgreSql;
using VeriScan.Api.Authentication;
using VeriScan.Application.Abstractions;
using VeriScan.Application.Contracts;
using VeriScan.Application.Services;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;
using Xunit;

namespace VeriScan.Api.PostgresTests;

/// <summary>PostgreSQL 真实事务测试集合，避免共享测试数据库的清理竞态。</summary>
[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class PostgreSqlTestGroup : ICollectionFixture<PostgreSqlFixture>
{
    public const string Name = "VeriScan PostgreSQL";
}

/// <summary>使用固定版本 PostgreSQL 镜像的 Testcontainers fixture。</summary>
public sealed class PostgreSqlFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer container = new PostgreSqlBuilder("postgres:16.10-alpine")
        .WithDatabase("veriscan_test")
        .WithUsername("veriscan")
        .WithPassword("veriscan-test-password")
        .Build();

    public string ConnectionString => container.GetConnectionString();

    public Task InitializeAsync()
    {
        return container.StartAsync();
    }

    public Task DisposeAsync()
    {
        return container.DisposeAsync().AsTask();
    }
}

/// <summary>验证取消接口在真实 PostgreSQL 行锁下的并发行为。</summary>
[Collection(PostgreSqlTestGroup.Name)]
public sealed class PostgresCancellationConcurrencyTests(PostgreSqlFixture fixture)
{
    private static readonly string[] ModerationScopes = ["moderation:submit", "moderation:read"];

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) }
    };

    [Fact]
    public async Task ConcurrentCancellationWithSameKeyCreatesOneOperationAndOutbox()
    {
        await using var factory = await CreateFactoryAsync(fixture);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "PostgreSQL 同键并发取消");
        var apiKey = await CreateApiKeyAsync(client, application.Id);
        var submitted = await SubmitAsync(client, apiKey, "postgres-concurrent-item");
        const string idempotencyKey = "postgres-cancel-key-20260902";

        var responses = await Task.WhenAll(
            Enumerable.Range(0, 12).Select(_ => SendCancellationAsync(
                client,
                submitted.RequestId,
                apiKey,
                idempotencyKey)));

        Assert.All(responses, result => Assert.Equal(HttpStatusCode.OK, result.StatusCode));
        Assert.NotEmpty(responses);
        Assert.All(responses, result => Assert.Equal(responses[0].Body, result.Body));

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
        Assert.Equal(
            12,
            await dbContext.ApiRequestEvents
                .AsNoTracking()
                .CountAsync(requestEvent =>
                    requestEvent.ApplicationId == application.Id &&
                    requestEvent.ModerationRequestId == submitted.RequestId &&
                    requestEvent.RouteTemplate == "/api/v1/moderation/batches/{requestId}/cancel"));
        Assert.Equal(
            1,
            await dbContext.ApiRequestEvents
                .AsNoTracking()
                .CountAsync(requestEvent =>
                    requestEvent.ApplicationId == application.Id &&
                    requestEvent.ModerationRequestId == submitted.RequestId &&
                    requestEvent.IdempotencyOutcome == "new_idempotent"));
        Assert.Equal(
            11,
            await dbContext.ApiRequestEvents
                .AsNoTracking()
                .CountAsync(requestEvent =>
                    requestEvent.ApplicationId == application.Id &&
                    requestEvent.ModerationRequestId == submitted.RequestId &&
                    requestEvent.IdempotencyOutcome == "replay"));
    }

    [Fact]
    public async Task WorkerClaimAndCancellationCannotProduceTwoTerminalStates()
    {
        await using var factory = await CreateFactoryAsync(fixture);
        using var client = factory.CreateClient();
        var application = await CreateApplicationAsync(client, "PostgreSQL 锁竞争");
        var apiKey = await CreateApiKeyAsync(client, application.Id);
        var submitted = await SubmitAsync(client, apiKey, "postgres-lock-race-item");
        const string idempotencyKey = "postgres-lock-race-key-20260902";
        using var ready = new CountdownEvent(2);
        using var start = new ManualResetEventSlim(false);

        var claimTask = Task.Run(async () =>
        {
            ready.Signal();
            start.Wait();
            await using var scope = factory.Services.CreateAsyncScope();
            var jobStore = scope.ServiceProvider.GetRequiredService<IModerationJobStore>();
            return await jobStore.ClaimNextAsync(
                "postgres-test-worker",
                DateTimeOffset.UtcNow,
                TimeSpan.FromMinutes(1),
                CancellationToken.None);
        });
        var cancelTask = Task.Run(async () =>
        {
            ready.Signal();
            start.Wait();
            return await SendCancellationAsync(
                client,
                submitted.RequestId,
                apiKey,
                idempotencyKey);
        });

        Assert.True(ready.Wait(TimeSpan.FromSeconds(10)));
        start.Set();
        var claim = await claimTask;
        var cancellation = await cancelTask;

        Assert.True(
            claim is null && cancellation.StatusCode == HttpStatusCode.OK ||
            claim is not null && cancellation.StatusCode == HttpStatusCode.Conflict,
            $"Claim={claim?.Status}, Cancel={(int)cancellation.StatusCode}: {cancellation.Body}");

        await using var scope = factory.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
        var job = await dbContext.ModerationJobs
            .AsNoTracking()
            .SingleAsync(item => item.RequestId == submitted.RequestId);
        var request = await dbContext.ModerationRequests
            .AsNoTracking()
            .SingleAsync(item => item.Id == submitted.RequestId);
        Assert.Equal(
            claim is null ? ModerationJobStatus.Cancelled : ModerationJobStatus.Processing,
            job.Status);
        Assert.Equal(
            claim is null
                ? ModerationProcessingStatus.Cancelled
                : ModerationProcessingStatus.Processing,
            request.ProcessingStatus);
        Assert.Equal(
            claim is null ? 1 : 0,
            await dbContext.IdempotentOperations
                .AsNoTracking()
                .CountAsync(item => item.TargetRequestId == submitted.RequestId));
        Assert.Equal(
            claim is null ? 1 : 0,
            await dbContext.OutboxEvents
                .AsNoTracking()
                .CountAsync(item =>
                    item.AggregateId == submitted.RequestId &&
                    item.EventType == "moderation.cancelled"));
    }

    private static async Task<PostgresApiTestFactory> CreateFactoryAsync(PostgreSqlFixture fixture)
    {
        var factory = new PostgresApiTestFactory(fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        return factory;
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
            displayName = "PostgreSQL 取消测试凭证",
            expiresAt = DateTimeOffset.UtcNow.AddHours(1),
            scopes = ModerationScopes
        });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.True(response.IsSuccessStatusCode, body);
        return JsonSerializer.Deserialize<ApiKeyCreatedResponse>(body, JsonOptions)!.ApiKey;
    }

    private static async Task<BatchModerationResponse> SubmitAsync(
        HttpClient client,
        string apiKey,
        string itemId)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/moderation/batches");
        request.Headers.Add("X-API-Key", apiKey);
        request.Content = JsonContent.Create(new
        {
            mode = "async",
            items = new[]
            {
                new
                {
                    id = itemId,
                    content = "等待 PostgreSQL 取消竞争的异步内容",
                    contentType = "plain_text"
                }
            }
        });
        var response = await client.SendAsync(request);
        var body = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        return JsonSerializer.Deserialize<BatchModerationResponse>(body, JsonOptions)!;
    }

    private static async Task<CancellationResponse> SendCancellationAsync(
        HttpClient client,
        Guid requestId,
        string apiKey,
        string idempotencyKey)
    {
        using var request = new HttpRequestMessage(
            HttpMethod.Post,
            $"/api/v1/moderation/batches/{requestId}/cancel");
        request.Headers.Add("X-API-Key", apiKey);
        request.Headers.Add("Idempotency-Key", idempotencyKey);
        var response = await client.SendAsync(request);
        return new CancellationResponse(
            response.StatusCode,
            await response.Content.ReadAsStringAsync());
    }

    private sealed record CancellationResponse(HttpStatusCode StatusCode, string Body);
}

/// <summary>为 PostgreSQL HTTP 测试替换数据库和管理员认证。</summary>
internal sealed class PostgresApiTestFactory(string connectionString)
    : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureAppConfiguration((_, configuration) =>
        {
            configuration.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Security:ApiKey:Pepper"] = "test-only-pepper-with-at-least-32-bytes-0001",
                ["Security:ApiKey:PepperVersion"] = "test-v1",
                ["Security:AiCredentials:MasterKey"] = "dmVyaXNjYW4tdGVzdC1tYXN0ZXIta2V5LTMyLWJ5dGU=",
                ["Security:ModerationDigests:ContentPepper"] = "test-content-pepper-with-at-least-32-bytes-0001",
                ["Security:ModerationDigests:IdempotencyPepper"] = "test-idempotency-pepper-with-at-least-32-bytes-0002",
                ["Security:ModerationDigests:KeyVersion"] = "test-v1",
                ["Database:AutoMigrate"] = "false",
                ["Outbox:Worker:Enabled"] = "false"
            });
        });
        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<VeriScanDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<VeriScanDbContext>>();
            services.AddDbContext<VeriScanDbContext>(options => options.UseNpgsql(connectionString));
            services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>();
            services.AddAuthentication()
                .AddScheme<AuthenticationSchemeOptions, PostgresTestAdminAuthenticationHandler>(
                    "PostgresTestAdmin",
                    _ => { });
            services.Configure<Microsoft.AspNetCore.Authorization.AuthorizationOptions>(options =>
            {
                var testAdminPolicy = new Microsoft.AspNetCore.Authorization.AuthorizationPolicyBuilder(
                        "PostgresTestAdmin")
                    .RequireAuthenticatedUser()
                    .Build();
                options.AddPolicy(AdminJwtOptions.Policy, testAdminPolicy);
                options.AddPolicy(AdminPolicies.Viewer, testAdminPolicy);
                options.AddPolicy(AdminPolicies.Operator, testAdminPolicy);
                options.AddPolicy(AdminPolicies.RuleEditor, testAdminPolicy);
                options.AddPolicy(AdminPolicies.AiConfigEditor, testAdminPolicy);
                options.AddPolicy(AdminPolicies.Publisher, testAdminPolicy);
                options.AddPolicy(AdminPolicies.Auditor, testAdminPolicy);
                options.AddPolicy(AdminPolicies.PlatformAdmin, testAdminPolicy);
            });
        });
    }

    public async Task InitializeDatabaseAsync()
    {
        await using var scope = Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
        await dbContext.Database.EnsureDeletedAsync();
        await dbContext.Database.MigrateAsync();
        if (await dbContext.RuleSetVersions.AnyAsync())
        {
            return;
        }

        var ruleSet = new RuleSetVersion("PostgreSQL 测试基础规则");
        ruleSet.ReplaceDraft(
            ruleSet.Name,
            [new WordRule(ruleSet.Id, "赌博", WordRuleType.Black, "gambling", 1.0m)]);
        var seedChecksum = RuleSetPolicyValidator.ComputeChecksum(ruleSet);
        ruleSet.RecordSuccessfulValidation(seedChecksum, DateTimeOffset.UtcNow);
        ruleSet.Publish(seedChecksum, DateTimeOffset.UtcNow);
        dbContext.RuleSetVersions.Add(ruleSet);
        await dbContext.SaveChangesAsync();
    }
}

/// <summary>只接受测试管理员 Bearer 令牌的认证处理器。</summary>
internal sealed class PostgresTestAdminAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var values) ||
            values.Count != 1 ||
            !values.ToString().StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var identity = new ClaimsIdentity(
            [new Claim("role", "veriscan-admin")],
            Scheme.Name);
        return Task.FromResult(
            AuthenticateResult.Success(
                new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
