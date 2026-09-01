using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

/// <summary>从请求、审核项和 AI 调用事实重建小时与日用量。</summary>
public sealed class UsageProjectionStore(VeriScanDbContext dbContext) : IUsageProjectionStore
{
    public Task<bool> ApplicationExistsAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Applications
            .AsNoTracking()
            .AnyAsync(application => application.Id == applicationId, cancellationToken);
    }

    public Task<bool> ApiKeyBelongsToApplicationAsync(
        Guid applicationId,
        Guid apiKeyId,
        CancellationToken cancellationToken)
    {
        return dbContext.ApplicationApiKeys
            .AsNoTracking()
            .AnyAsync(
                apiKey => apiKey.ApplicationId == applicationId && apiKey.Id == apiKeyId,
                cancellationToken);
    }

    public async Task<UsageRebuildData> RebuildAsync(
        Guid applicationId,
        Guid? apiKeyId,
        DateTimeOffset from,
        DateTimeOffset through,
        CancellationToken cancellationToken)
    {
        var fromValue = from;
        var throughValue = through;
        var tenantId = await dbContext.Applications
            .AsNoTracking()
            .Where(application => application.Id == applicationId)
            .Select(application => (Guid?)application.TenantId)
            .SingleOrDefaultAsync(cancellationToken)
            ?? throw new InvalidOperationException("重建用量时应用已不存在。");
        var requests = await dbContext.ModerationRequests
            .AsNoTracking()
            .Where(request =>
                request.ApplicationId == applicationId &&
                request.SubmittedAt >= fromValue &&
                request.SubmittedAt < throughValue &&
                (apiKeyId == null || request.CreatedByApiKeyId == apiKeyId))
            .Select(request => new RequestFact(
                request.Id,
                request.CreatedByApiKeyId,
                request.SubmittedAt))
            .ToArrayAsync(cancellationToken);

        var items = await (
                from item in dbContext.ModerationItems.AsNoTracking()
                join request in dbContext.ModerationRequests.AsNoTracking()
                    on item.RequestId equals request.Id
                where request.ApplicationId == applicationId &&
                      request.SubmittedAt >= fromValue &&
                      request.SubmittedAt < throughValue &&
                      (apiKeyId == null || request.CreatedByApiKeyId == apiKeyId)
                select new ItemFact(
                    request.CreatedByApiKeyId,
                    request.SubmittedAt,
                    item.Decision))
            .ToArrayAsync(cancellationToken);

        var invocations = await (
                from invocation in dbContext.AiInvocations.AsNoTracking()
                join request in dbContext.ModerationRequests.AsNoTracking()
                    on invocation.ModerationRequestId equals request.Id
                where request.ApplicationId == applicationId &&
                      request.SubmittedAt >= fromValue &&
                      request.SubmittedAt < throughValue &&
                      (apiKeyId == null || request.CreatedByApiKeyId == apiKeyId)
                select new InvocationFact(
                    request.CreatedByApiKeyId,
                    request.SubmittedAt,
                    invocation.Outcome,
                    invocation.FailureCode,
                    invocation.InputTokens,
                    invocation.OutputTokens))
            .ToArrayAsync(cancellationToken);

        var replays = await dbContext.ApiRequestEvents
            .AsNoTracking()
            .Where(requestEvent =>
                requestEvent.ApplicationId == applicationId &&
                requestEvent.OccurredAt >= fromValue &&
                requestEvent.OccurredAt < throughValue &&
                requestEvent.IdempotencyOutcome != "new" &&
                requestEvent.IdempotencyOutcome != "new_idempotent" &&
                requestEvent.ApiKeyId.HasValue &&
                (apiKeyId == null || requestEvent.ApiKeyId == apiKeyId))
            .Select(requestEvent => new ReplayFact(
                requestEvent.ApiKeyId!.Value,
                requestEvent.OccurredAt))
            .ToArrayAsync(cancellationToken);

        var hourly = new Dictionary<UsageKey, UsageCounter>();
        var daily = new Dictionary<UsageKey, UsageCounter>();

        foreach (var request in requests)
        {
            AddRequest(hourly, request.CreatedByApiKeyId, request.SubmittedAt);
            AddRequest(daily, request.CreatedByApiKeyId, request.SubmittedAt, daily: true);
        }

        foreach (var item in items)
        {
            AddItem(hourly, item.CreatedByApiKeyId, item.SubmittedAt, item.Decision);
            AddItem(daily, item.CreatedByApiKeyId, item.SubmittedAt, item.Decision, daily: true);
        }

        foreach (var invocation in invocations)
        {
            AddInvocation(hourly, invocation);
            AddInvocation(daily, invocation, daily: true);
        }

        foreach (var replay in replays)
        {
            GetCounter(hourly, replay.ApiKeyId, replay.OccurredAt).IdempotencyReplayCount++;
            GetCounter(daily, replay.ApiKeyId, replay.OccurredAt, daily: true).IdempotencyReplayCount++;
        }

        var firstHour = FloorHour(fromValue);
        var firstDay = FloorDay(fromValue);
        var oldHourly = await dbContext.UsageHourly
            .AsNoTracking()
            .Where(usage =>
                usage.ApplicationId == applicationId &&
                usage.BucketStart >= firstHour &&
                usage.BucketStart < throughValue &&
                (apiKeyId == null || usage.ApiKeyId == apiKeyId))
            .ToArrayAsync(cancellationToken);
        var oldDaily = await dbContext.UsageDaily
            .AsNoTracking()
            .Where(usage =>
                usage.ApplicationId == applicationId &&
                usage.BucketStart >= firstDay &&
                usage.BucketStart < throughValue &&
                (apiKeyId == null || usage.ApiKeyId == apiKeyId))
            .ToArrayAsync(cancellationToken);

        dbContext.UsageHourly.RemoveRange(oldHourly);
        dbContext.UsageDaily.RemoveRange(oldDaily);

        foreach (var pair in hourly)
        {
            var usage = new UsageHourly(
                tenantId,
                applicationId,
                pair.Key.ApiKeyId,
                pair.Key.BucketStart);
            pair.Value.Apply(usage);
            dbContext.UsageHourly.Add(usage);
        }

        foreach (var pair in daily)
        {
            var usage = new UsageDaily(
                tenantId,
                applicationId,
                pair.Key.ApiKeyId,
                pair.Key.BucketStart);
            pair.Value.Apply(usage);
            dbContext.UsageDaily.Add(usage);
        }

        if (dbContext.Database.IsRelational())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        else
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }

        return new UsageRebuildData(
            fromValue,
            throughValue,
            hourly.Count,
            daily.Count,
            requests.LongLength,
            items.LongLength,
            invocations.LongLength);
    }

    private static void AddRequest(
        IDictionary<UsageKey, UsageCounter> counters,
        Guid apiKeyId,
        DateTimeOffset occurredAt,
        bool daily = false)
    {
        GetCounter(counters, apiKeyId, occurredAt, daily).RequestCount++;
    }

    private static void AddItem(
        IDictionary<UsageKey, UsageCounter> counters,
        Guid apiKeyId,
        DateTimeOffset occurredAt,
        ModerationDecision? decision,
        bool daily = false)
    {
        var counter = GetCounter(counters, apiKeyId, occurredAt, daily);
        counter.ItemCount++;
        switch (decision)
        {
            case ModerationDecision.Pass:
                counter.PassCount++;
                break;
            case ModerationDecision.Reject:
                counter.RejectCount++;
                break;
            case ModerationDecision.Review:
                counter.ReviewCount++;
                break;
        }
    }

    private static void AddInvocation(
        IDictionary<UsageKey, UsageCounter> counters,
        InvocationFact invocation,
        bool daily = false)
    {
        var counter = GetCounter(counters, invocation.ApiKeyId, invocation.SubmittedAt, daily);
        counter.AiCallCount++;
        if (!string.Equals(invocation.Outcome, "Succeeded", StringComparison.Ordinal) ||
            invocation.FailureCode is not null)
        {
            counter.AiFailureCount++;
        }

        if (invocation.InputTokens is { } inputTokens)
        {
            counter.InputTokens = (counter.InputTokens ?? 0) + inputTokens;
        }

        if (invocation.OutputTokens is { } outputTokens)
        {
            counter.OutputTokens = (counter.OutputTokens ?? 0) + outputTokens;
        }
    }

    private static UsageCounter GetCounter(
        IDictionary<UsageKey, UsageCounter> counters,
        Guid apiKeyId,
        DateTimeOffset occurredAt,
        bool daily = false)
    {
        var bucketStart = daily ? FloorDay(occurredAt) : FloorHour(occurredAt);
        var key = new UsageKey(apiKeyId, bucketStart);
        if (!counters.TryGetValue(key, out var counter))
        {
            counter = new UsageCounter();
            counters.Add(key, counter);
        }

        return counter;
    }

    private static DateTimeOffset FloorHour(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return new DateTimeOffset(
            utc.Year,
            utc.Month,
            utc.Day,
            utc.Hour,
            0,
            0,
            TimeSpan.Zero);
    }

    private static DateTimeOffset FloorDay(DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return new DateTimeOffset(utc.Year, utc.Month, utc.Day, 0, 0, 0, TimeSpan.Zero);
    }

    private readonly record struct RequestFact(
        Guid Id,
        Guid CreatedByApiKeyId,
        DateTimeOffset SubmittedAt);

    private readonly record struct ItemFact(
        Guid CreatedByApiKeyId,
        DateTimeOffset SubmittedAt,
        ModerationDecision? Decision);

    private readonly record struct InvocationFact(
        Guid ApiKeyId,
        DateTimeOffset SubmittedAt,
        string Outcome,
        string? FailureCode,
        int? InputTokens,
        int? OutputTokens);

    private readonly record struct ReplayFact(Guid ApiKeyId, DateTimeOffset OccurredAt);

    private readonly record struct UsageKey(Guid ApiKeyId, DateTimeOffset BucketStart);

    private sealed class UsageCounter
    {
        public long RequestCount { get; set; }

        public long IdempotencyReplayCount { get; set; }

        public long ItemCount { get; set; }

        public long PassCount { get; set; }

        public long RejectCount { get; set; }

        public long ReviewCount { get; set; }

        public long AiCallCount { get; set; }

        public long AiFailureCount { get; set; }

        public long? InputTokens { get; set; }

        public long? OutputTokens { get; set; }

        public void Apply(UsageHourly usage)
        {
            usage.Replace(
                RequestCount,
                IdempotencyReplayCount,
                ItemCount,
                PassCount,
                RejectCount,
                ReviewCount,
                AiCallCount,
                AiFailureCount,
                InputTokens,
                OutputTokens,
                DateTimeOffset.UtcNow);
        }

        public void Apply(UsageDaily usage)
        {
            usage.Replace(
                RequestCount,
                IdempotencyReplayCount,
                ItemCount,
                PassCount,
                RejectCount,
                ReviewCount,
                AiCallCount,
                AiFailureCount,
                InputTokens,
                OutputTokens,
                DateTimeOffset.UtcNow);
        }
    }
}
