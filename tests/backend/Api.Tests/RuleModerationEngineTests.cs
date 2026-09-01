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

    [Fact]
    public void CompiledMatcherFindsOverlappingTermsAfterUnicodeNormalization()
    {
        var engine = new RuleModerationEngine();
        var rules = new WordRule[]
        {
            new(Guid.Empty, "ＢＡＤ", WordRuleType.Suspicious, "risk", 0.6m),
            new(Guid.Empty, "ad", WordRuleType.Black, "critical", 1m)
        };

        var result = engine.GetOrCompile("ruleset@test-overlap", rules).Evaluate("bad");

        Assert.Equal(ModerationDecision.Reject, result.Decision);
        Assert.Contains(result.Categories, category => category.Code == "critical");
    }

    [Fact]
    public void PublishedRevisionReturnsSameCompiledPolicyAcrossCalls()
    {
        var engine = new RuleModerationEngine();
        var rules = new WordRule[]
        {
            new(Guid.Empty, "诈骗", WordRuleType.Black, "fraud", 1m)
        };

        var first = engine.GetOrCompile("ruleset@stable", rules);
        var second = engine.GetOrCompile("ruleset@stable", []);

        Assert.Same(first, second);
        Assert.Equal(ModerationDecision.Reject, second.Evaluate("这是诈骗").Decision);
    }

    [Fact]
    public void NormalizerRemovesSeparatorsAndKeepsOriginalEvidenceRange()
    {
        var normalized = RuleTextNormalizer.Normalize("加\u200B  微信");

        Assert.Equal("加微信", normalized.Value);
        Assert.Equal(3, normalized.Spans.Count);
        Assert.Equal(0, normalized.Spans[0].OriginalStart);
        Assert.Equal(5, normalized.Spans[2].OriginalStart);
    }

    [Fact]
    public void TraditionalSimplifiedProfileCanBeEnabledExplicitly()
    {
        var engine = new RuleModerationEngine();
        var rules = new WordRule[]
        {
            new(Guid.Empty, "国家", WordRuleType.Black, "policy", 1m)
        };

        var disabled = engine.Evaluate("國家", rules);
        var enabled = engine.Evaluate(
            "國家",
            rules,
            [],
            [],
            RuleNormalizationOptions.ForProfile(RuleNormalizationProfile.TraditionalSimplified));

        Assert.NotEqual(ModerationDecision.Reject, disabled.Decision);
        Assert.Equal(ModerationDecision.Reject, enabled.Decision);
    }

    [Fact]
    public void RegexRuleUsesSafeEngineAndReturnsOriginalEvidence()
    {
        var engine = new RuleModerationEngine();
        var regexRules = new RegexRule[]
        {
            new(Guid.Empty, @"1(?:3|4)\d{9}", RuleAction.ForceReview, "contact", 0.8m)
        };

        var result = engine.Evaluate("联系电话：13812345678", [], regexRules, []);

        Assert.Equal(ModerationDecision.Review, result.Decision);
        Assert.False(result.RequiresAi);
        var evidence = Assert.Single(result.EvidenceDetails);
        Assert.Equal("13812345678", evidence.Quote);
        Assert.Equal(5, evidence.OriginalStart);
        Assert.Equal("regex", evidence.RuleKind);
    }

    [Fact]
    public void DangerousRegexCannotBePublished()
    {
        var rule = new RegexRule(
            Guid.Empty,
            "(a+)+$",
            RuleAction.RiskSignal,
            "risk",
            0.5m,
            engineMode: RegexRuleEngineMode.Backtracking);

        var validation = RegexRuleSafetyValidator.Validate(rule);

        Assert.False(validation.Valid);
        Assert.Equal("REGEX_NESTED_QUANTIFIER", validation.Code);
    }

    [Fact]
    public void CombinationRuleRequiresAllTermsInsideConfiguredWindow()
    {
        var engine = new RuleModerationEngine();
        var combinationRules = new CombinationRule[]
        {
            new(
                Guid.Empty,
                "导流组合",
                ["加微信", "优惠"],
                RuleAction.ForceReview,
                "contact",
                0.8m,
                windowSize: 12)
        };

        var matched = engine.Evaluate("优惠活动请加微信", [], [], combinationRules);
        var outsideWindow = engine.Evaluate("优惠活动请提供更多信息后再联系我们加微信", [], [], combinationRules);

        Assert.Equal(ModerationDecision.Review, matched.Decision);
        Assert.False(matched.RequiresAi);
        Assert.Empty(outsideWindow.EvidenceDetails);
        Assert.True(outsideWindow.RequiresAi);
    }

    [Fact]
    public void MonitorOnlyRuleDoesNotChangeTerminalDecision()
    {
        var engine = new RuleModerationEngine();
        var rules = new WordRule[]
        {
            new(
                Guid.Empty,
                "实验词",
                WordRuleType.Suspicious,
                "experiment",
                0.2m,
                RuleAction.MonitorOnly)
        };

        var result = engine.Evaluate("实验词", rules);

        Assert.Equal(ModerationDecision.Review, result.Decision);
        Assert.True(result.RequiresAi);
        Assert.Contains("RULE_MONITOR_ONLY", result.ReasonCodes);
        Assert.Single(result.EvidenceDetails);
    }
}
