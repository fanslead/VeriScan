using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

/// <summary>为取消操作建立任务行锁和同库事务。</summary>
public sealed class ModerationCancellationStore(VeriScanDbContext dbContext)
    : IModerationCancellationStore
{
    private static readonly SemaphoreSlim InMemoryTransactionGate = new(1, 1);

    public async Task<IModerationCancellationTransaction> BeginAsync(
        CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            return new Transaction(dbContext, transaction, releaseInMemoryGate: false);
        }

        await InMemoryTransactionGate.WaitAsync(cancellationToken);
        return new Transaction(dbContext, databaseTransaction: null, releaseInMemoryGate: true);
    }

    private sealed class Transaction(
        VeriScanDbContext dbContext,
        IDbContextTransaction? databaseTransaction,
        bool releaseInMemoryGate) : IModerationCancellationTransaction
    {
        private bool _committed;
        private bool _disposed;

        public Task<ModerationJob?> GetJobForUpdateAsync(
            Guid applicationId,
            Guid requestId,
            CancellationToken cancellationToken)
        {
            if (dbContext.Database.ProviderName?.Contains("Npgsql", StringComparison.Ordinal) == true)
            {
                return dbContext.ModerationJobs
                    .FromSqlInterpolated($"""
                        SELECT *
                        FROM moderation_jobs
                        WHERE "ApplicationId" = {applicationId}
                          AND "RequestId" = {requestId}
                        FOR UPDATE
                        """)
                    .Include(job => job.Request)!
                    .ThenInclude(request => request!.Items)
                    .SingleOrDefaultAsync(cancellationToken);
            }

            return dbContext.ModerationJobs
                .Include(job => job.Request)!
                .ThenInclude(request => request!.Items)
                .SingleOrDefaultAsync(
                    job => job.ApplicationId == applicationId && job.RequestId == requestId,
                    cancellationToken);
        }

        public Task<IdempotentOperation?> GetOperationAsync(
            Guid applicationId,
            Guid requestId,
            string operation,
            string idempotencyKeyDigest,
            DateTimeOffset now,
            CancellationToken cancellationToken)
        {
            return dbContext.IdempotentOperations.SingleOrDefaultAsync(
                item => item.ApplicationId == applicationId &&
                        item.TargetRequestId == requestId &&
                        item.Operation == operation &&
                        item.IdempotencyKeyDigest == idempotencyKeyDigest &&
                        item.ExpiresAt > now,
                cancellationToken);
        }

        public Task AddOperationAsync(
            IdempotentOperation operation,
            CancellationToken cancellationToken)
        {
            return dbContext.IdempotentOperations.AddAsync(operation, cancellationToken).AsTask();
        }

        public Task SaveChangesAsync(CancellationToken cancellationToken)
        {
            return dbContext.SaveChangesAsync(cancellationToken);
        }

        public async Task CommitAsync(CancellationToken cancellationToken)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            if (databaseTransaction is not null)
            {
                await databaseTransaction.CommitAsync(cancellationToken);
            }

            _committed = true;
        }

        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            try
            {
                if (!_committed && databaseTransaction is not null)
                {
                    await databaseTransaction.RollbackAsync(CancellationToken.None);
                }

                if (databaseTransaction is not null)
                {
                    await databaseTransaction.DisposeAsync();
                }
            }
            finally
            {
                if (releaseInMemoryGate)
                {
                    InMemoryTransactionGate.Release();
                }
            }
        }
    }
}
