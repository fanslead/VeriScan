using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;
using VeriScan.Api.Workers;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;
using VeriScan.Infrastructure.Persistence.Repositories;

namespace VeriScan.Api.Tests;

public sealed class OutboxWorkerTests
{
    [Fact]
    public async Task ClaimUsesLeaseAndRejectsWrongLockToken()
    {
        var databaseName = $"outbox-claim-{Guid.CreateVersion7():N}";
        var eventId = await SeedEventAsync(databaseName, "application.updated");
        var now = DateTimeOffset.UtcNow;

        await using var firstContext = CreateContext(databaseName);
        var firstStore = new OutboxStore(firstContext);
        var claimed = await firstStore.ClaimAvailableAsync(
            now,
            10,
            TimeSpan.FromMinutes(1),
            "worker-a",
            CancellationToken.None);

        Assert.Single(claimed);
        Assert.Equal("worker-a", claimed[0].LockToken);

        await using var secondContext = CreateContext(databaseName);
        var secondStore = new OutboxStore(secondContext);
        var blocked = await secondStore.ClaimAvailableAsync(
            now,
            10,
            TimeSpan.FromMinutes(1),
            "worker-b",
            CancellationToken.None);
        Assert.Empty(blocked);

        Assert.False(await firstStore.TryCompleteAsync(
            eventId,
            "wrong-token",
            "test-consumer",
            now,
            CancellationToken.None));
        Assert.True(await firstStore.TryCompleteAsync(
            eventId,
            "worker-a",
            "test-consumer",
            now,
            CancellationToken.None));

        await using var verificationContext = CreateContext(databaseName);
        var savedEvent = await verificationContext.OutboxEvents
            .AsNoTracking()
            .SingleAsync(item => item.Id == eventId);
        Assert.NotNull(savedEvent.PublishedAt);
        Assert.Single(await verificationContext.UsageConsumedEvents
            .AsNoTracking()
            .Where(item => item.OutboxEventId == eventId)
            .ToArrayAsync());
    }

    [Fact]
    public async Task FailedEventKeepsPendingStateAndGetsBackoffTimestamp()
    {
        var databaseName = $"outbox-failure-{Guid.CreateVersion7():N}";
        var eventId = await SeedEventAsync(databaseName, "application.updated");
        var now = DateTimeOffset.UtcNow;

        await using var context = CreateContext(databaseName);
        var store = new OutboxStore(context);
        var claimed = await store.ClaimAvailableAsync(
            now,
            1,
            TimeSpan.FromMinutes(1),
            "worker-a",
            CancellationToken.None);
        Assert.Single(claimed);

        var retryAt = now.AddMinutes(5);
        Assert.True(await store.TryFailAsync(
            eventId,
            "worker-a",
            "OUTBOX_TEST_FAILURE",
            retryAt,
            CancellationToken.None));

        var savedEvent = await context.OutboxEvents
            .AsNoTracking()
            .SingleAsync(item => item.Id == eventId);
        Assert.Null(savedEvent.PublishedAt);
        Assert.Equal("OUTBOX_TEST_FAILURE", savedEvent.LastErrorCode);
        Assert.Equal(retryAt, savedEvent.AvailableAt);
    }

    [Fact]
    public async Task WorkerRebuildsUsageForModerationCompletionAndRecordsConsumption()
    {
        var projection = new RecordingUsageProjectionService();
        await using var factory = new ApiTestFactory(services =>
        {
            services.RemoveAll<IUsageProjectionService>();
            services.AddSingleton<IUsageProjectionService>(projection);
            services.PostConfigure<OutboxWorkerOptions>(options => options.Enabled = true);
        });
        await factory.SeedRulesAsync();
        _ = factory.CreateClient();

        var application = new ApplicationEntity(
            Guid.Empty,
            $"app_{Guid.CreateVersion7():N}",
            "Outbox Worker 测试应用",
            "test");
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
            dbContext.Applications.Add(application);
            dbContext.OutboxEvents.Add(new OutboxEvent(
                "moderation.completed",
                "moderation_request",
                Guid.CreateVersion7(),
                Guid.Empty,
                application.Id,
                "{}",
                DateTimeOffset.UtcNow));
            await dbContext.SaveChangesAsync();
        }

        await EventuallyAsync(
            async () =>
            {
                await using var scope = factory.Services.CreateAsyncScope();
                var dbContext = scope.ServiceProvider.GetRequiredService<VeriScanDbContext>();
                return await dbContext.OutboxEvents
                    .AsNoTracking()
                    .AnyAsync(item =>
                        item.ApplicationId == application.Id && item.PublishedAt != null);
            });

        Assert.Equal(1, projection.Calls);
        await using var verificationScope = factory.Services.CreateAsyncScope();
        var verificationContext = verificationScope.ServiceProvider
            .GetRequiredService<VeriScanDbContext>();
        var savedEvent = await verificationContext.OutboxEvents
            .AsNoTracking()
            .SingleAsync(item => item.ApplicationId == application.Id);
        Assert.NotNull(savedEvent.PublishedAt);
        Assert.Single(await verificationContext.UsageConsumedEvents
            .AsNoTracking()
            .Where(item => item.OutboxEventId == savedEvent.Id)
            .ToArrayAsync());
    }

    private static VeriScanDbContext CreateContext(string databaseName)
    {
        var options = new DbContextOptionsBuilder<VeriScanDbContext>()
            .UseInMemoryDatabase(databaseName)
            .Options;
        return new VeriScanDbContext(options);
    }

    private static async Task<Guid> SeedEventAsync(string databaseName, string eventType)
    {
        await using var context = CreateContext(databaseName);
        await context.Database.EnsureCreatedAsync();
        var outboxEvent = new OutboxEvent(
            eventType,
            "application",
            Guid.CreateVersion7(),
            null,
            null,
            "{}",
            DateTimeOffset.UtcNow);
        context.OutboxEvents.Add(outboxEvent);
        await context.SaveChangesAsync();
        return outboxEvent.Id;
    }

    private static async Task EventuallyAsync(
        Func<Task<bool>> predicate,
        int attempts = 50)
    {
        for (var attempt = 0; attempt < attempts; attempt++)
        {
            if (await predicate())
            {
                return;
            }

            await Task.Delay(100);
        }

        Assert.Fail("等待 Outbox Worker 处理事件超时。");
    }

    private sealed class RecordingUsageProjectionService : IUsageProjectionService
    {
        public int Calls => Volatile.Read(ref calls);

        private int calls;

        public Task<UsageRebuildData> RebuildAsync(
            Guid applicationId,
            Guid? apiKeyId,
            DateTimeOffset? from,
            DateTimeOffset? through,
            CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref calls);
            return Task.FromResult(new UsageRebuildData(
                from ?? DateTimeOffset.UtcNow.AddDays(-7),
                through ?? DateTimeOffset.UtcNow,
                0,
                0,
                0,
                0,
                0));
        }
    }
}
