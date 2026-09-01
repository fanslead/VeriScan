using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class ModerationStore(VeriScanDbContext dbContext) : IModerationStore
{
    public Task AddAsync(ModerationRequest request, CancellationToken cancellationToken)
    {
        return dbContext.ModerationRequests.AddAsync(request, cancellationToken).AsTask();
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

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
