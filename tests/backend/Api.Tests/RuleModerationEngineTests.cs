using VeriScan.Application.Services;
using VeriScan.Domain.Entities;

namespace VeriScan.Api.Tests;

public sealed class RuleModerationEngineTests
{
    [Fact]
    public void WhiteRuleFromAnotherCategoryDoesNotSuppressSuspiciousRule()
    {
        var engine = new RuleModerationEngine();
        var rules = new WordRule[]
        {
            new(Guid.Empty, "加微信", WordRuleType.Suspicious, "contact", 0.6m),
            new(Guid.Empty, "明鉴", WordRuleType.White, "product", 0.1m)
        };

        var result = engine.Evaluate("明鉴请加微信", rules);

        Assert.Equal(ModerationDecision.Review, result.Decision);
        Assert.True(result.RequiresAi);
        Assert.Equal("policy_required", result.ReviewSource);
    }

    [Fact]
    public void WhiteRuleFromSameCategorySuppressesSignalButStillRequiresSemanticReview()
    {
        var engine = new RuleModerationEngine();
        var rules = new WordRule[]
        {
            new(Guid.Empty, "加微信", WordRuleType.Suspicious, "contact", 0.6m),
            new(Guid.Empty, "官方客服", WordRuleType.White, "contact", 0.1m)
        };

        var result = engine.Evaluate("官方客服请加微信", rules);

        Assert.Equal(ModerationDecision.Review, result.Decision);
        Assert.True(result.RequiresAi);
        Assert.Contains("RULE_CONTEXT_EXCEPTION", result.ReasonCodes);
    }
}
