using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class ApplicationWebhookStore(VeriScanDbContext dbContext)
    : IApplicationWebhookStore
{
    public Task<ApplicationEntity?> GetApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return dbContext.Applications.SingleOrDefaultAsync(
            application => application.Id == applicationId,
            cancellationToken);
    }

    public Task<ApplicationWebhook?> GetByApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return dbContext.ApplicationWebhooks.SingleOrDefaultAsync(
            webhook => webhook.ApplicationId == applicationId,
            cancellationToken);
    }

    public Task AddAsync(ApplicationWebhook webhook, CancellationToken cancellationToken)
    {
        return dbContext.ApplicationWebhooks.AddAsync(webhook, cancellationToken).AsTask();
    }

    public Task AddPublicationAsync(
        WebhookPublication publication,
        CancellationToken cancellationToken)
    {
        return dbContext.WebhookPublications.AddAsync(publication, cancellationToken).AsTask();
    }

    public Task<WebhookPublication?> GetTestAsync(
        Guid applicationId,
        Guid testId,
        CancellationToken cancellationToken)
    {
        return dbContext.WebhookPublications
            .AsNoTracking()
            .SingleOrDefaultAsync(
                publication => publication.Id == testId &&
                               publication.ApplicationId == applicationId &&
                               publication.Kind == WebhookPublicationKind.Test,
                cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DataConcurrencyException();
        }
    }
}

/// <summary>使用数据库租约领取 Webhook 发布事件。</summary>
public sealed class WebhookPublicationStore(VeriScanDbContext dbContext)
    : IWebhookPublicationStore
{
    private static readonly SemaphoreSlim InMemoryMutationGate = new(1, 1);

    public async Task<IReadOnlyList<WebhookPublication>> ClaimAvailableAsync(
        DateTimeOffset now,
        int limit,
        TimeSpan leaseDuration,
        string leaseOwner,
        CancellationToken cancellationToken)
    {
        ValidateClaim(limit, leaseDuration, leaseOwner);
        var claimedAt = now.ToUniversalTime();
        var leaseExpiresAt = claimedAt.Add(leaseDuration);
        if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
        {
            await using var transaction = await dbContext.Database.BeginTransactionAsync(
                cancellationToken);
            var publications = await dbContext.WebhookPublications
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM webhook_publications
                    WHERE ("Status" = 'Queued' OR "Status" = 'Delivering')
                      AND "AvailableAt" <= {claimedAt}
                      AND ("LeaseExpiresAt" IS NULL OR "LeaseExpiresAt" <= {claimedAt})
                    ORDER BY "AvailableAt", "CreatedAt", "Id"
                    FOR UPDATE SKIP LOCKED
                    LIMIT {limit}
                    """)
                .ToArrayAsync(cancellationToken);
            foreach (var publication in publications)
            {
                publication.Claim(leaseOwner, claimedAt, leaseDuration);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return publications;
        }

        await InMemoryMutationGate.WaitAsync(cancellationToken);
        try
        {
            var publications = await dbContext.WebhookPublications
                .Where(publication =>
                    (publication.Status == WebhookPublicationStatus.Queued ||
                     publication.Status == WebhookPublicationStatus.Delivering) &&
                    publication.AvailableAt <= claimedAt &&
                    (publication.LeaseExpiresAt == null ||
                     publication.LeaseExpiresAt <= claimedAt))
                .OrderBy(publication => publication.AvailableAt)
                .ThenBy(publication => publication.CreatedAt)
                .ThenBy(publication => publication.Id)
                .Take(limit)
                .ToArrayAsync(cancellationToken);
            foreach (var publication in publications)
            {
                publication.Claim(leaseOwner, claimedAt, leaseDuration);
            }

            await dbContext.SaveChangesAsync(cancellationToken);
            return publications;
        }
        finally
        {
            InMemoryMutationGate.Release();
        }
    }

    public Task<ApplicationWebhook?> GetConfigurationAsync(
        Guid applicationWebhookId,
        CancellationToken cancellationToken)
    {
        return dbContext.ApplicationWebhooks.SingleOrDefaultAsync(
            webhook => webhook.Id == applicationWebhookId,
            cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static void ValidateClaim(int limit, TimeSpan leaseDuration, string leaseOwner)
    {
        if (limit is < 1 or > 1000)
        {
            throw new ArgumentOutOfRangeException(nameof(limit));
        }

        ArgumentOutOfRangeException.ThrowIfLessThanOrEqual(
            leaseDuration,
            TimeSpan.Zero,
            nameof(leaseDuration));
        if (string.IsNullOrWhiteSpace(leaseOwner) || leaseOwner.Trim().Length > 128)
        {
            throw new ArgumentException("租约持有者不能为空且不能超过 128 个字符。", nameof(leaseOwner));
        }
    }
}
