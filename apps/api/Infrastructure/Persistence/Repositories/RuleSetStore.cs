using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class RuleSetStore(VeriScanDbContext dbContext) : IRuleSetStore
{
    public Task AddAsync(RuleSetVersion ruleSet, CancellationToken cancellationToken)
    {
        return dbContext.RuleSetVersions.AddAsync(ruleSet, cancellationToken).AsTask();
    }

    public Task<RuleSetVersion?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        return dbContext.RuleSetVersions
            .Include(ruleSet => ruleSet.Rules)
            .Include(ruleSet => ruleSet.Applications)
            .SingleOrDefaultAsync(ruleSet => ruleSet.Id == id, cancellationToken);
    }

    public Task<RuleSetVersion?> GetByPublicRevisionIdAsync(
        string publicRevisionId,
        CancellationToken cancellationToken)
    {
        return dbContext.RuleSetVersions
            .Include(ruleSet => ruleSet.Rules)
            .Include(ruleSet => ruleSet.Applications)
            .SingleOrDefaultAsync(
                ruleSet => ruleSet.PublicRevisionId == publicRevisionId,
                cancellationToken);
    }

    public Task<RuleSetVersion?> GetLatestPublishedAsync(CancellationToken cancellationToken)
    {
        return dbContext.RuleSetVersions
            .AsNoTracking()
            .Where(ruleSet => ruleSet.Status == RuleSetStatus.Published)
            .OrderByDescending(ruleSet => ruleSet.PublishedAt)
            .ThenByDescending(ruleSet => ruleSet.Id)
            .FirstOrDefaultAsync(cancellationToken);
    }

    public Task<RuleSetVersion?> GetBoundForApplicationAsync(
        Guid applicationId,
        CancellationToken cancellationToken)
    {
        return dbContext.RuleSetVersions
            .AsNoTracking()
            .Include(ruleSet => ruleSet.Rules)
            .SingleOrDefaultAsync(
                ruleSet => ruleSet.Applications.Any(application => application.Id == applicationId),
                cancellationToken);
    }

    public async Task<IReadOnlyList<RuleSetVersion>> ListAsync(CancellationToken cancellationToken)
    {
        return await dbContext.RuleSetVersions
            .AsNoTracking()
            .Include(ruleSet => ruleSet.Rules)
            .Include(ruleSet => ruleSet.Applications)
            .OrderBy(ruleSet => ruleSet.Status == RuleSetStatus.Published ? 0 : 1)
            .ThenByDescending(ruleSet => ruleSet.UpdatedAt)
            .ToArrayAsync(cancellationToken);
    }

    public Task<int> CountBindingsAsync(Guid ruleSetVersionId, CancellationToken cancellationToken)
    {
        return dbContext.Applications.CountAsync(
            application => application.RuleSetVersionId == ruleSetVersionId,
            cancellationToken);
    }

    public async Task ReplaceDraftAsync(
        RuleSetVersion ruleSet,
        string name,
        IReadOnlyCollection<WordRule> rules,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!dbContext.Database.IsRelational())
            {
                await ReplaceDraftCoreAsync(ruleSet, name, rules, cancellationToken);
                return;
            }

            await using var transaction = await dbContext.Database.BeginTransactionAsync(cancellationToken);
            await ReplaceDraftCoreAsync(ruleSet, name, rules, cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException)
        {
            throw new DataConcurrencyException();
        }
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

    private async Task ReplaceDraftCoreAsync(
        RuleSetVersion ruleSet,
        string name,
        IReadOnlyCollection<WordRule> rules,
        CancellationToken cancellationToken)
    {
        var previousRules = ruleSet.Rules.ToArray();
        ruleSet.ReplaceDraft(name, []);
        dbContext.WordRules.RemoveRange(previousRules);
        await dbContext.SaveChangesAsync(cancellationToken);
        ruleSet.ReplaceDraft(name, rules);
        dbContext.WordRules.AddRange(rules);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
