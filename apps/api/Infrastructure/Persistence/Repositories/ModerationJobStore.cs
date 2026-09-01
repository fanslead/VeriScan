using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class ModerationJobStore(VeriScanDbContext dbContext) : IModerationJobStore
{
    public async Task<ModerationJob?> ClaimNextAsync(
        string workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken)
    {
        var usePostgres = dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true;
        await using var transaction = usePostgres
            ? await dbContext.Database.BeginTransactionAsync(cancellationToken)
            : null;
        ModerationJob? job;
        if (usePostgres)
        {
            job = await dbContext.ModerationJobs
                .FromSqlInterpolated($"""
                    SELECT *
                    FROM moderation_jobs
                    WHERE
                        (("Status" = 'Pending' OR "Status" = 'RetryWait') AND "AvailableAt" <= {now})
                        OR ("Status" = 'Processing' AND "LeaseExpiresAt" <= {now})
                    ORDER BY "Priority" DESC, "AvailableAt", "CreatedAt"
                    FOR UPDATE SKIP LOCKED
                    LIMIT 1
                    """)
                .Include(candidate => candidate.Request)!
                .ThenInclude(request => request!.Items)
                .SingleOrDefaultAsync(cancellationToken);
        }
        else
        {
            job = await dbContext.ModerationJobs
                .Include(candidate => candidate.Request)!
                .ThenInclude(request => request!.Items)
                .Where(candidate =>
                    ((candidate.Status == ModerationJobStatus.Pending ||
                      candidate.Status == ModerationJobStatus.RetryWait) &&
                     candidate.AvailableAt <= now) ||
                    (candidate.Status == ModerationJobStatus.Processing &&
                     candidate.LeaseExpiresAt <= now))
                .OrderByDescending(candidate => candidate.Priority)
                .ThenBy(candidate => candidate.AvailableAt)
                .ThenBy(candidate => candidate.CreatedAt)
                .FirstOrDefaultAsync(cancellationToken);
        }

        if (job is null)
        {
            if (transaction is not null)
            {
                await transaction.RollbackAsync(cancellationToken);
            }
            return null;
        }

        job.Claim(workerId, now, leaseDuration);
        job.Request?.StartProcessing();
        foreach (var item in job.Request?.Items ?? [])
        {
            item.StartProcessing();
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }
        return job;
    }

    public Task<ModerationJob?> GetByRequestIdAsync(
        Guid applicationId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return dbContext.ModerationJobs
            .Include(job => job.Request)!
            .ThenInclude(request => request!.Items)
            .SingleOrDefaultAsync(
                job => job.ApplicationId == applicationId && job.RequestId == requestId,
                cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
