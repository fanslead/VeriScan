using Microsoft.EntityFrameworkCore;
using VeriScan.Application.Services;
using VeriScan.Domain.Entities;
using VeriScan.Infrastructure.Persistence;

namespace VeriScan.Api.Tests;

public sealed class RuleSetPolicyValidatorTests
{
    [Fact]
    public async Task InitializerPublishesChecksumComputedFromSeedContent()
    {
        var options = new DbContextOptionsBuilder<VeriScanDbContext>()
            .UseInMemoryDatabase($"seed-checksum-{Guid.CreateVersion7():N}")
            .Options;
        await using var dbContext = new VeriScanDbContext(options);
        var initializer = new DatabaseInitializer(dbContext);

        await initializer.InitializeAsync(CancellationToken.None);

        var ruleSet = await dbContext.RuleSetVersions.Include(item => item.Rules).SingleAsync();
        var expected = RuleSetPolicyValidator.ComputeChecksum(ruleSet.Name, ruleSet.Rules);
        Assert.Equal(expected, ruleSet.LastValidatedChecksum);
        Assert.Equal(expected, ruleSet.PublishedChecksum);
    }

    [Fact]
    public void InvalidRegexPreventsRuleSetValidation()
    {
        var ruleSet = new RuleSetVersion("正则校验");
        ruleSet.ReplaceDraft(
            ruleSet.Name,
            [new WordRule(ruleSet.Id, "正常词", WordRuleType.Suspicious, "risk", 0.5m)],
            [new RegexRule(
                ruleSet.Id,
                "(a+)+$",
                RuleAction.RiskSignal,
                "risk",
                0.5m,
                engineMode: RegexRuleEngineMode.Backtracking)],
            []);

        var validation = RuleSetPolicyValidator.Validate(ruleSet);

        Assert.False(validation.Valid);
        Assert.Contains(validation.Issues, issue => issue.Code == "REGEX_NESTED_QUANTIFIER");
    }

    [Fact]
    public void CombinationRuleIsIncludedInValidationCountAndChecksum()
    {
        var ruleSet = new RuleSetVersion("组合校验");
        ruleSet.ReplaceDraft(
            ruleSet.Name,
            [],
            [],
            [new CombinationRule(
                ruleSet.Id,
                "导流组合",
                ["加微信", "优惠"],
                RuleAction.ForceReview,
                "contact",
                0.8m)]);

        var validation = RuleSetPolicyValidator.Validate(ruleSet);

        Assert.True(validation.Valid);
        Assert.Equal(1, validation.RuleCount);
        Assert.Equal(64, validation.Checksum.Length);
    }
}
