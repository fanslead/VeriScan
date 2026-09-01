using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

/// <summary>将运营事实加入当前 DbContext，提交由业务仓储统一负责。</summary>
public sealed class OperationalFactStore(VeriScanDbContext dbContext) : IOperationalFactStore
{
    public Task AddAuditAsync(AuditEvent auditEvent, CancellationToken cancellationToken)
    {
        return dbContext.AuditEvents.AddAsync(auditEvent, cancellationToken).AsTask();
    }

    public Task AddApiRequestAsync(ApiRequestEvent requestEvent, CancellationToken cancellationToken)
    {
        return dbContext.ApiRequestEvents.AddAsync(requestEvent, cancellationToken).AsTask();
    }

    public Task AddAiInvocationAsync(AiInvocation invocation, CancellationToken cancellationToken)
    {
        return dbContext.AiInvocations.AddAsync(invocation, cancellationToken).AsTask();
    }

    public Task AddOutboxAsync(OutboxEvent outboxEvent, CancellationToken cancellationToken)
    {
        return dbContext.OutboxEvents.AddAsync(outboxEvent, cancellationToken).AsTask();
    }
}

/// <summary>读取尚未投递的 Outbox 事件。</summary>
public sealed class OutboxStore(VeriScanDbContext dbContext) : IOutboxStore
{
    private static readonly SemaphoreSlim InMemoryMutationGate = new(1, 1);

    public async Task<IReadOnlyList<OutboxEvent>> ListAvailableAsync(
        DateTimeOffset now,
        int limit,
        CancellationToken cancellationToken)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        return await dbContext.OutboxEvents
            .AsNoTracking()
            .Where(outboxEvent =>
                outboxEvent.PublishedAt == null &&
                outboxEvent.AvailableAt <= now &&
                (outboxEvent.LockedUntil == null || outboxEvent.LockedUntil <= now))
            .OrderBy(outboxEvent => outboxEvent.OccurredAt)
            .ThenBy(outboxEvent => outboxEvent.Id)
            .Take(limit)
            .ToArrayAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<OutboxEvent>> ClaimAvailableAsync(
        DateTimeOffset now,
        int limit,
        TimeSpan leaseDuration,
        string lockToken,
        CancellationToken cancellationToken)
    {
        ValidateMutationArguments(limit, leaseDuration, lockToken);
        var occurredAt = now.ToUniversalTime();
        var lockedUntil = occurredAt.Add(leaseDuration);
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
            var events = await dbContext.OutboxEvents
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM "outbox_events"
                    WHERE "PublishedAt" IS NULL
                      AND "AvailableAt" <= {occurredAt}
                      AND ("LockedUntil" IS NULL OR "LockedUntil" <= {occurredAt})
                    ORDER BY "OccurredAt", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT {limit}
                    """)
                .ToArrayAsync(cancellationToken);
            foreach (var outboxEvent in events)
            {
                outboxEvent.Claim(lockToken, lockedUntil);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return events;
        }

        await InMemoryMutationGate.WaitAsync(cancellationToken);
        try
        {
            var events = await dbContext.OutboxEvents
                .Where(outboxEvent =>
                    outboxEvent.PublishedAt == null &&
                    outboxEvent.AvailableAt <= occurredAt &&
                    (outboxEvent.LockedUntil == null || outboxEvent.LockedUntil <= occurredAt))
                .OrderBy(outboxEvent => outboxEvent.OccurredAt)
                .ThenBy(outboxEvent => outboxEvent.Id)
                .Take(limit)
                .ToArrayAsync(cancellationToken);
            foreach (var outboxEvent in events)
            {
                outboxEvent.Claim(lockToken, lockedUntil);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return events;
        }
        finally
        {
            InMemoryMutationGate.Release();
        }
    }

    public async Task<bool> TryCompleteAsync(
        Guid outboxEventId,
        string lockToken,
        string consumerName,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        ValidateMutationArguments(1, TimeSpan.FromSeconds(1), lockToken);
        if (string.IsNullOrWhiteSpace(consumerName) || consumerName.Trim().Length > 96)
        {
            throw new ArgumentException("消费方名称不能为空且不能超过 96 个字符。", nameof(consumerName));
        }

        if (dbContext.Database.IsRelational())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
            var completed = await CompleteCoreAsync(
                outboxEventId,
                lockToken,
                consumerName.Trim(),
                completedAt.ToUniversalTime(),
                cancellationToken);
            if (completed)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return completed;
        }

        await InMemoryMutationGate.WaitAsync(cancellationToken);
        try
        {
            return await CompleteCoreAsync(
                outboxEventId,
                lockToken,
                consumerName.Trim(),
                completedAt.ToUniversalTime(),
                cancellationToken);
        }
        finally
        {
            InMemoryMutationGate.Release();
        }
    }

    public async Task<bool> TryFailAsync(
        Guid outboxEventId,
        string lockToken,
        string errorCode,
        DateTimeOffset availableAt,
        CancellationToken cancellationToken)
    {
        ValidateMutationArguments(1, TimeSpan.FromSeconds(1), lockToken);
        if (string.IsNullOrWhiteSpace(errorCode) || errorCode.Trim().Length > 96)
        {
            throw new ArgumentException("失败代码不能为空且不能超过 96 个字符。", nameof(errorCode));
        }

        if (dbContext.Database.IsRelational())
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
            var failed = await FailCoreAsync(
                outboxEventId,
                lockToken,
                errorCode.Trim(),
                availableAt.ToUniversalTime(),
                cancellationToken);
            if (failed)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return failed;
        }

        await InMemoryMutationGate.WaitAsync(cancellationToken);
        try
        {
            return await FailCoreAsync(
                outboxEventId,
                lockToken,
                errorCode.Trim(),
                availableAt.ToUniversalTime(),
                cancellationToken);
        }
        finally
        {
            InMemoryMutationGate.Release();
        }
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private async Task<bool> CompleteCoreAsync(
        Guid outboxEventId,
        string lockToken,
        string consumerName,
        DateTimeOffset completedAt,
        CancellationToken cancellationToken)
    {
        var outboxEvent = await dbContext.OutboxEvents
            .SingleOrDefaultAsync(item => item.Id == outboxEventId, cancellationToken);
        if (outboxEvent is null ||
            outboxEvent.IsPublished ||
            !string.Equals(outboxEvent.LockToken, lockToken, StringComparison.Ordinal))
        {
            return false;
        }

        var consumed = await dbContext.UsageConsumedEvents
            .AnyAsync(
                item => item.ConsumerName == consumerName && item.OutboxEventId == outboxEventId,
                cancellationToken);
        if (!consumed)
        {
            dbContext.UsageConsumedEvents.Add(
                new UsageConsumedEvent(consumerName, outboxEventId, completedAt));
        }

        outboxEvent.MarkPublished(completedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task<bool> FailCoreAsync(
        Guid outboxEventId,
        string lockToken,
        string errorCode,
        DateTimeOffset availableAt,
        CancellationToken cancellationToken)
    {
        var outboxEvent = await dbContext.OutboxEvents
            .SingleOrDefaultAsync(item => item.Id == outboxEventId, cancellationToken);
        if (outboxEvent is null ||
            outboxEvent.IsPublished ||
            !string.Equals(outboxEvent.LockToken, lockToken, StringComparison.Ordinal))
        {
            return false;
        }

        outboxEvent.MarkFailed(errorCode, availableAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        return true;
    }

    private static void ValidateMutationArguments(int limit, TimeSpan leaseDuration, string lockToken)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            leaseDuration,
            TimeSpan.Zero,
            nameof(leaseDuration));

        if (string.IsNullOrWhiteSpace(lockToken) || lockToken.Trim().Length > 128)
        {
            throw new ArgumentException("租约令牌不能为空且不能超过 128 个字符。", nameof(lockToken));
        }
    }
}
