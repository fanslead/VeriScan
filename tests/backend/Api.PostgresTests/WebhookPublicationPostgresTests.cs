using Microsoft.EntityFrameworkCore;
using Npgsql;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;
using VeriScan.Infrastructure.Persistence.Repositories;
using Xunit;

namespace VeriScan.Api.PostgresTests;

/// <summary>验证 Webhook 发布队列在真实 PostgreSQL 下的租约与去重约束。</summary>
[Collection(PostgreSqlTestGroup.Name)]
public sealed class WebhookPublicationPostgresTests(PostgreSqlFixture fixture)
{
    private static readonly string[] WorkerNames =
    [
        "postgres-webhook-worker-a",
        "postgres-webhook-worker-b"
    ];

    [Fact]
    public async Task ConcurrentClaimsFromIndependentDbContextsOnlyClaimOnePublication()
    {
        await using var factory = await CreateFactoryAsync(fixture);
        var seeded = await SeedWebhookAndPublicationAsync(
            fixture.ConnectionString,
            "postgres-webhook-claim");
        var now = DateTimeOffset.UtcNow;

        await using var firstContext = CreateContext(fixture.ConnectionString);
        await using var secondContext = CreateContext(fixture.ConnectionString);
        var firstStore = new WebhookPublicationStore(firstContext);
        var secondStore = new WebhookPublicationStore(secondContext);
        using var start = new ManualResetEventSlim(false);
        var firstClaim = Task.Run(async () =>
        {
            start.Wait();
            return await firstStore.ClaimAvailableAsync(
                now,
                1,
                TimeSpan.FromMinutes(1),
                "postgres-webhook-worker-a",
                CancellationToken.None);
        });
        var secondClaim = Task.Run(async () =>
        {
            start.Wait();
            return await secondStore.ClaimAvailableAsync(
                now,
                1,
                TimeSpan.FromMinutes(1),
                "postgres-webhook-worker-b",
                CancellationToken.None);
        });

        start.Set();
        var claims = await Task.WhenAll(firstClaim, secondClaim);

        Assert.Single(claims.SelectMany(items => items));
        Assert.Contains(claims, items => items.Count == 1);
        Assert.Contains(claims, items => items.Count == 0);

        await using var verificationContext = CreateContext(fixture.ConnectionString);
        var saved = await verificationContext.WebhookPublications
            .AsNoTracking()
            .SingleAsync(publication => publication.Id == seeded.PublicationId);
        Assert.Equal(WebhookPublicationStatus.Queued, saved.Status);
        Assert.NotNull(saved.LeaseOwner);
        Assert.Contains(saved.LeaseOwner, WorkerNames);
        Assert.NotNull(saved.LeaseExpiresAt);
    }

    [Fact]
    public async Task DuplicateModerationTerminalDeduplicationKeyIsRejectedByPostgreSql()
    {
        await using var factory = await CreateFactoryAsync(fixture);
        var seeded = await SeedWebhookAsync(
            fixture.ConnectionString,
            "postgres-webhook-dedup");
        var occurredAt = DateTimeOffset.UtcNow;
        var deduplicationKey = $"moderation-terminal:{seeded.RequestId:N}";

        await using var firstContext = CreateContext(fixture.ConnectionString);
        firstContext.WebhookPublications.Add(CreatePublication(
            seeded,
            Guid.CreateVersion7(),
            deduplicationKey,
            occurredAt));
        await firstContext.SaveChangesAsync();

        await using var secondContext = CreateContext(fixture.ConnectionString);
        secondContext.WebhookPublications.Add(CreatePublication(
            seeded,
            Guid.CreateVersion7(),
            deduplicationKey,
            occurredAt.AddMilliseconds(1)));

        var exception = await Assert.ThrowsAsync<DbUpdateException>(
            () => secondContext.SaveChangesAsync());

        Assert.IsType<PostgresException>(exception.InnerException);
        Assert.Equal(
            PostgresErrorCodes.UniqueViolation,
            ((PostgresException)exception.InnerException!).SqlState);

        await using var verificationContext = CreateContext(fixture.ConnectionString);
        Assert.Equal(
            1,
            await verificationContext.WebhookPublications
                .AsNoTracking()
                .CountAsync(publication => publication.DeduplicationKey == deduplicationKey));
    }

    [Fact]
    public async Task MigrationCreatesWebhookTablesAndUniqueDeduplicationIndex()
    {
        await using var factory = await CreateFactoryAsync(fixture);
        await using var connection = new NpgsqlConnection(fixture.ConnectionString);
        await connection.OpenAsync();

        await using var tableCommand = connection.CreateCommand();
        tableCommand.CommandText = """
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
              AND table_name IN ('application_webhooks', 'webhook_publications')
            """;
        Assert.Equal(2L, (long)(await tableCommand.ExecuteScalarAsync())!);

        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            SELECT COUNT(*)
            FROM pg_indexes
            WHERE schemaname = 'public'
              AND tablename = 'webhook_publications'
              AND indexdef LIKE '%UNIQUE%'
              AND indexdef LIKE '%DeduplicationKey%'
            """;
        Assert.Equal(1L, (long)(await indexCommand.ExecuteScalarAsync())!);
    }

    private static async Task<(Guid ApplicationId, Guid WebhookId, Guid RequestId, Guid PublicationId)>
        SeedWebhookAndPublicationAsync(string connectionString, string suffix)
    {
        var seeded = await SeedWebhookAsync(connectionString, suffix);
        var publicationId = Guid.CreateVersion7();
        await using var context = CreateContext(connectionString);
        context.WebhookPublications.Add(CreatePublication(
            seeded,
            publicationId,
            $"moderation-terminal:{seeded.RequestId:N}",
            DateTimeOffset.UtcNow.AddMinutes(-1)));
        await context.SaveChangesAsync();
        return (seeded.ApplicationId, seeded.WebhookId, seeded.RequestId, publicationId);
    }

    private static async Task<(Guid ApplicationId, Guid WebhookId, Guid RequestId)> SeedWebhookAsync(
        string connectionString,
        string suffix)
    {
        var tenantId = Guid.CreateVersion7();
        var application = new ApplicationEntity(
            tenantId,
            $"app_{Guid.CreateVersion7():N}",
            $"Webhook PostgreSQL {suffix}",
            "test");
        var webhook = new ApplicationWebhook(
            tenantId,
            application.Id,
            "https://receiver.example.test/veriscan",
            $"svix-app-{Guid.CreateVersion7():N}",
            $"svix-endpoint-{Guid.CreateVersion7():N}",
            DateTimeOffset.UtcNow);
        var request = new ModerationRequest(
            tenantId,
            application.Id,
            Guid.CreateVersion7(),
            "async",
            "test-policy",
            null,
            null,
            DateTimeOffset.UtcNow,
            ModerationProcessingStatus.Completed);

        await using var context = CreateContext(connectionString);
        context.Applications.Add(application);
        context.ApplicationWebhooks.Add(webhook);
        context.ModerationRequests.Add(request);
        await context.SaveChangesAsync();
        return (application.Id, webhook.Id, request.Id);
    }

    private static WebhookPublication CreatePublication(
        (Guid ApplicationId, Guid WebhookId, Guid RequestId) seeded,
        Guid publicationId,
        string deduplicationKey,
        DateTimeOffset createdAt)
    {
        return new WebhookPublication(
            publicationId,
            Guid.Empty,
            seeded.ApplicationId,
            seeded.WebhookId,
            1,
            "svix-app-test",
            "svix-endpoint-test",
            WebhookPublicationKind.Notification,
            "moderation.completed",
            "{\"schemaVersion\":\"1.0\",\"eventType\":\"moderation.completed\"}",
            deduplicationKey,
            createdAt);
    }

    private static VeriScanDbContext CreateContext(string connectionString)
    {
        var options = new DbContextOptionsBuilder<VeriScanDbContext>()
            .UseNpgsql(connectionString)
            .Options;
        return new VeriScanDbContext(options);
    }

    private static async Task<PostgresApiTestFactory> CreateFactoryAsync(
        PostgreSqlFixture fixture)
    {
        var factory = new PostgresApiTestFactory(fixture.ConnectionString);
        await factory.InitializeDatabaseAsync();
        return factory;
    }
}
