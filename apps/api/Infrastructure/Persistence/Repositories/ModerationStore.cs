using Microsoft.EntityFrameworkCore;
using Npgsql;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class ModerationStore(VeriScanDbContext dbContext) : IModerationStore
{
    public async Task<bool> TryReserveAsync(
        ModerationRequest request,
        CancellationToken cancellationToken)
    {
        await dbContext.ModerationRequests.AddAsync(request, cancellationToken);
        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            return true;
        }
        catch (DbUpdateException exception) when (
            request.IdempotencyKeyDigest is not null && IsIdempotencyConflict(exception))
        {
            dbContext.Entry(request).State = EntityState.Detached;
            return false;
        }
    }

    public Task<ModerationRequest?> GetByIdAsync(
        Guid applicationId,
        Guid requestId,
        CancellationToken cancellationToken)
    {
        return dbContext.ModerationRequests
            .Include(request => request.Items)
            .SingleOrDefaultAsync(
                request => request.ApplicationId == applicationId && request.Id == requestId,
                cancellationToken);
    }

    public Task<ModerationRequest?> GetByIdempotencyKeyAsync(
        Guid applicationId,
        string idempotencyKeyDigest,
        CancellationToken cancellationToken)
    {
        return dbContext.ModerationRequests
            .AsNoTracking()
            .Include(request => request.Items)
            .SingleOrDefaultAsync(
                request => request.ApplicationId == applicationId &&
                           request.IdempotencyKeyDigest == idempotencyKeyDigest,
                cancellationToken);
    }

    public Task AddItemAsync(ModerationItem item, CancellationToken cancellationToken)
    {
        return dbContext.ModerationItems.AddAsync(item, cancellationToken).AsTask();
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }

    private static bool IsIdempotencyConflict(DbUpdateException exception)
    {
        return exception.InnerException is PostgresException
        {
            SqlState: PostgresErrorCodes.UniqueViolation,
            ConstraintName: "IX_moderation_requests_ApplicationId_IdempotencyKeyDigest"
        };
    }
}
