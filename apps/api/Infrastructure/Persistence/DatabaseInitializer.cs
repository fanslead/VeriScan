using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Services;
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

        if (await dbContext.RuleSetVersions.AnyAsync(cancellationToken))
        {
            return;
        }

        var ruleSet = new RuleSetVersion("明鉴基础规则");
        ruleSet.ReplaceDraft(
            ruleSet.Name,
            [
                new WordRule(ruleSet.Id, "赌博", WordRuleType.Black, "gambling", 1.0m),
                new WordRule(ruleSet.Id, "诈骗", WordRuleType.Black, "fraud", 1.0m),
                new WordRule(ruleSet.Id, "暴恐", WordRuleType.Black, "violence", 1.0m),
                new WordRule(ruleSet.Id, "色情", WordRuleType.Black, "sexual", 1.0m),
                new WordRule(ruleSet.Id, "加微信", WordRuleType.Suspicious, "contact", 0.6m),
                new WordRule(ruleSet.Id, "联系方式", WordRuleType.Suspicious, "contact", 0.6m),
                new WordRule(ruleSet.Id, "明鉴", WordRuleType.White, "product", 0.1m),
                new WordRule(ruleSet.Id, "veriscan", WordRuleType.White, "product", 0.1m)
            ]);
        var seedChecksum = RuleSetPolicyValidator.ComputeChecksum(ruleSet.Name, ruleSet.Rules);
        ruleSet.RecordSuccessfulValidation(seedChecksum, DateTimeOffset.UtcNow);
        ruleSet.Publish(seedChecksum, DateTimeOffset.UtcNow);
        dbContext.RuleSetVersions.Add(ruleSet);
        await dbContext.SaveChangesAsync(cancellationToken);
    }
}
