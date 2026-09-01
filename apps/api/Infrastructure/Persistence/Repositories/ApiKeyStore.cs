using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class ApiKeyStore(VeriScanDbContext dbContext) : IApiKeyStore
{
    public Task AddAsync(ApplicationApiKey apiKey, CancellationToken cancellationToken)
    {
        return dbContext.ApplicationApiKeys.AddAsync(apiKey, cancellationToken).AsTask();
    }

    public Task<ApplicationApiKey?> GetByIdAsync(
        Guid applicationId,
        Guid keyId,
        CancellationToken cancellationToken)
    {
        return dbContext.ApplicationApiKeys
            .SingleOrDefaultAsync(
                key => key.ApplicationId == applicationId && key.Id == keyId,
                cancellationToken);
    }

    public Task<ApplicationApiKey?> GetByPublicKeyIdAsync(
        string publicKeyId,
        CancellationToken cancellationToken)
    {
        return dbContext.ApplicationApiKeys
            .Include(key => key.Application)
            .SingleOrDefaultAsync(key => key.PublicKeyId == publicKeyId, cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationApiKey>> ListByApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return await dbContext.ApplicationApiKeys
            .Where(key => key.ApplicationId == applicationId)
            .OrderByDescending(key => key.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
