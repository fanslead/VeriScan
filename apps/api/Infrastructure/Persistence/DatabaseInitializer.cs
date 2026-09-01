using Microsoft.EntityFrameworkCore;
using VeriScan.Domain.Entities;

namespace VeriScan.Infrastructure.Persistence;

public sealed class DatabaseInitializer(VeriScanDbContext dbContext)
{
    public async Task InitializeAsync(CancellationToken cancellationToken)
    {
        if (dbContext.Database.IsRelational())
        {
            await dbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            await dbContext.Database.EnsureCreatedAsync(cancellationToken);
        }

        if (await dbContext.WordRules.AnyAsync(cancellationToken))
        {
            return;
        }

        dbContext.WordRules.AddRange(
            new WordRule("赌博", WordRuleType.Black, "gambling", 1.0m),
            new WordRule("诈骗", WordRuleType.Black, "fraud", 1.0m),
            new WordRule("暴恐", WordRuleType.Black, "violence", 1.0m),
            new WordRule("色情", WordRuleType.Black, "sexual", 1.0m),
            new WordRule("加微信", WordRuleType.Suspicious, "contact", 0.6m),
            new WordRule("联系方式", WordRuleType.Suspicious, "contact", 0.6m),
            new WordRule("明鉴", WordRuleType.White, "product", 0.1m),
            new WordRule("veriscan", WordRuleType.White, "product", 0.1m));
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
