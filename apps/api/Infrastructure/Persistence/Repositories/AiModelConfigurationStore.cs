using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class AiModelConfigurationStore(VeriScanDbContext dbContext) : IAiModelConfigurationStore
{
    public async Task AddAsync(AiModelConfiguration configuration, CancellationToken cancellationToken)
    {
        await dbContext.AiModelConfigurations.AddAsync(configuration, cancellationToken);
    }

    public Task<AiModelConfiguration?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.AiModelConfigurations.SingleOrDefaultAsync(
            configuration => configuration.Id == id,
            cancellationToken);
    }

    public Task<AiModelConfiguration?> GetActiveAsync(CancellationToken cancellationToken)
    {
        return dbContext.AiModelConfigurations.SingleOrDefaultAsync(
            configuration => configuration.IsActive,
            cancellationToken);
    }

    public async Task<IReadOnlyList<AiModelConfiguration>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.AiModelConfigurations
            .AsNoTracking()
            .OrderByDescending(configuration => configuration.IsActive)
            .ThenByDescending(configuration => configuration.UpdatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public async Task ActivateExclusiveAsync(
        AiModelConfiguration configuration,
        DateTimeOffset activatedAt,
        CancellationToken cancellationToken)
    {
        if (!dbContext.Database.IsRelational())
        {
            var active = await GetActiveAsync(cancellationToken);
            if (active is not null && active.Id != configuration.Id)
            {
                active.Deactivate(activatedAt);
            }

            configuration.Activate(activatedAt);
            await dbContext.SaveChangesAsync(cancellationToken);
            return;
        }

        await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
        await dbContext.AiModelConfigurations
            .Where(candidate => candidate.IsActive && candidate.Id != configuration.Id)
            .ExecuteUpdateAsync(
                setters => setters
                    .SetProperty(candidate => candidate.IsActive, false)
                    .SetProperty(candidate => candidate.UpdatedAt, activatedAt),
                cancellationToken);
        configuration.Activate(activatedAt);
        await dbContext.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        return dbContext.SaveChangesAsync(cancellationToken);
    }
}
