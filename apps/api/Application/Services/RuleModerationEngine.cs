using System.Globalization;
using System.Text;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IRuleModerationEngine
{
    RuleEvaluation Evaluate(string content, IReadOnlyList<WordRule> rules);
}

public sealed record RuleEvaluation(
    ModerationDecision Decision,
    string? ReviewSource,
    bool Degraded,
    decimal? RiskScore,
    string? ScoreSource,
    string Route,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<RuleCategory> Categories);

public sealed record RuleCategory(string Code, decimal? RiskScore);

public sealed class RuleModerationEngine : IRuleModerationEngine
{
    public RuleEvaluation Evaluate(string content, IReadOnlyList<WordRule> rules)
    {
        var normalizedContent = Normalize(content);
        var matches = rules
            .Where(rule => normalizedContent.Contains(Normalize(rule.Term), StringComparison.Ordinal))
            .ToArray();
        var blackMatches = matches.Where(rule => rule.Type == WordRuleType.Black).ToArray();
        var suspiciousMatches = matches.Where(rule => rule.Type == WordRuleType.Suspicious).ToArray();
        var whiteMatches = matches.Where(rule => rule.Type == WordRuleType.White).ToArray();

        if (blackMatches.Length > 0)
        {
            return new RuleEvaluation(
                ModerationDecision.Reject,
                null,
                false,
                0.99m,
                "deterministic_rule",
                "local_rules",
                ["RULE_BLACK_WORD"],
                blackMatches.Select(rule => new RuleCategory(rule.Category, 0.99m)).Distinct().ToArray());
        }

        var hasRelatedWhiteMatch = whiteMatches.Any(whiteRule =>
            suspiciousMatches.Any(suspiciousRule =>
                string.Equals(whiteRule.Category, suspiciousRule.Category, StringComparison.Ordinal)));

        if (suspiciousMatches.Length > 0 && !hasRelatedWhiteMatch)
        {
            return new RuleEvaluation(
                ModerationDecision.Review,
                "policy_required",
                false,
                null,
                null,
                "local_rules",
                ["RULE_SUSPICIOUS_WORD", "CALLER_REVIEW_REQUIRED"],
                suspiciousMatches.Select(rule => new RuleCategory(rule.Category, null)).Distinct().ToArray());
        }

        if (whiteMatches.Length > 0)
        {
            return new RuleEvaluation(
                ModerationDecision.Pass,
                null,
                false,
                0.01m,
                "deterministic_rule",
                "local_rules",
                ["RULE_WHITE_WORD"],
                []);
        }

        return new RuleEvaluation(
            ModerationDecision.Review,
            "policy_required",
            false,
            null,
            null,
            "local_rules",
            ["AI_ROUTE_NOT_CONFIGURED", "CALLER_REVIEW_REQUIRED"],
            []);
    }

    private static string Normalize(string value)
    {
        return value.Normalize(NormalizationForm.FormKC).ToUpper(CultureInfo.InvariantCulture);
    }
}
