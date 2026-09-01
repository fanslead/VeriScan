using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Abstractions;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence.Repositories;

public sealed class WordRuleStore(VeriScanDbContext dbContext) : IWordRuleStore
{
    public async Task<IReadOnlyList<WordRule>> GetEnabledAsync(CancellationToken cancellationToken)
    {
        return await dbContext.WordRules
            .AsNoTracking()
            .Where(rule => rule.IsEnabled)
            .OrderByDescending(rule => rule.Weight)
            .ToListAsync(cancellationToken);
    }
}
