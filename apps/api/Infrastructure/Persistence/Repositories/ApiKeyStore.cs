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

    public Task<ApiKeyVerificationData?> GetVerificationDataAsync(
        string publicKeyId,
        CancellationToken cancellationToken)
    {
        return dbContext.ApplicationApiKeys
            .AsNoTracking()
            .Where(key => key.PublicKeyId == publicKeyId && key.Application != null)
            .Select(key => new ApiKeyVerificationData(
                key.Id,
                key.TenantId,
                key.ApplicationId,
                key.PublicKeyId,
                key.SecretDigest,
                key.PepperVersion,
                key.ScopesText,
                key.EnvironmentName,
                key.Status,
                key.NotBefore,
                key.ExpiresAt,
                key.Application!.Status))
            .SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApplicationApiKey>> ListByApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        var keys = await dbContext.ApplicationApiKeys
            .AsNoTracking()
            .Where(key => key.ApplicationId == applicationId)
            .OrderByDescending(key => key.CreatedAt)
            .ToListAsync(cancellationToken);
        var lastUsedByKey = await dbContext.ModerationRequests
            .AsNoTracking()
            .Where(request => request.ApplicationId == applicationId)
            .GroupBy(request => request.CreatedByApiKeyId)
            .Select(group => new
            {
                KeyId = group.Key,
                LastUsedAt = group.Max(request => request.SubmittedAt)
            })
            .ToDictionaryAsync(item => item.KeyId, item => item.LastUsedAt, cancellationToken);
        foreach (var key in keys)
        {
            if (lastUsedByKey.TryGetValue(key.Id, out var lastUsedAt))
            {
                key.MarkUsed(lastUsedAt);
            }
        }

        return keys;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
