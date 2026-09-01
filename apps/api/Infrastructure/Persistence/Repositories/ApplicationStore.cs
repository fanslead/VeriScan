using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class ApplicationStore(VeriScanDbContext dbContext) : IApplicationStore
{
    public Task AddAsync(ApplicationEntity application, CancellationToken cancellationToken)
    {
        return dbContext.Applications.AddAsync(application, cancellationToken).AsTask();
    }

    public Task<ApplicationEntity?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.Applications
            .Include(application => application.ApiKeys)
            .SingleOrDefaultAsync(application => application.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationEntity>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.Applications
            .Include(application => application.ApiKeys)
            .OrderByDescending(application => application.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
