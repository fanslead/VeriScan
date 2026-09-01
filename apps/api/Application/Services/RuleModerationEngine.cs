using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IRuleModerationEngine
{
    RuleEvaluation Evaluate(string content, IReadOnlyList<WordRule> rules);

    ICompiledRulePolicy GetOrCompile(string revisionId, IReadOnlyList<WordRule> rules);
}

public interface ICompiledRulePolicy
{
    RuleEvaluation Evaluate(string content);
}

public sealed record RuleEvaluation(
    ModerationDecision Decision,
    bool RequiresAi,
    string? ReviewSource,
    bool Degraded,
    decimal? RiskScore,
    string? ScoreSource,
    string Route,
    IReadOnlyList<string> ReasonCodes,
    IReadOnlyList<RuleCategory> Categories,
    IReadOnlyList<string> Evidence);

public sealed record RuleCategory(string Code, decimal? RiskScore);

public sealed class RuleModerationEngine : IRuleModerationEngine
{
    private const int MaximumCachedPolicies = 64;
    private readonly ConcurrentDictionary<string, Lazy<CompiledRulePolicy>> policies =
        new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> insertionOrder = new();

    public RuleEvaluation Evaluate(string content, IReadOnlyList<WordRule> rules)
    {
        return CompiledRulePolicy.Create(rules).Evaluate(content);
    }

    public ICompiledRulePolicy GetOrCompile(string revisionId, IReadOnlyList<WordRule> rules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revisionId);
        var candidate = new Lazy<CompiledRulePolicy>(
            () => CompiledRulePolicy.Create(rules),
            LazyThreadSafetyMode.ExecutionAndPublication);
        var policy = policies.GetOrAdd(revisionId, candidate);
        if (ReferenceEquals(policy, candidate))
        {
            insertionOrder.Enqueue(revisionId);
            TrimCache();
        }

        return policy.Value;
    }

    private void TrimCache()
    {
        while (policies.Count > MaximumCachedPolicies && insertionOrder.TryDequeue(out var revisionId))
        {
            policies.TryRemove(revisionId, out _);
        }
    }

    private sealed class CompiledRulePolicy(
        IReadOnlyList<WordRule> rules,
        IReadOnlyList<MatcherNode> nodes) : ICompiledRulePolicy
    {
        public static CompiledRulePolicy Create(IReadOnlyList<WordRule> sourceRules)
        {
            var enabledRules = sourceRules.Where(rule => rule.IsEnabled).ToArray();
            var matcherNodes = new List<MatcherNode> { new() };
            for (var ruleIndex = 0; ruleIndex < enabledRules.Length; ruleIndex++)
            {
                var term = Normalize(enabledRules[ruleIndex].Term);
                if (term.Length == 0)
                {
                    continue;
                }

                var state = 0;
                foreach (var character in term)
                {
                    if (!matcherNodes[state].Transitions.TryGetValue(character, out var nextState))
                    {
                        nextState = matcherNodes.Count;
                        matcherNodes[state].Transitions.Add(character, nextState);
                        matcherNodes.Add(new MatcherNode());
                    }

                    state = nextState;
                }

                matcherNodes[state].Outputs.Add(ruleIndex);
            }

            BuildFailureLinks(matcherNodes);
            return new CompiledRulePolicy(enabledRules, matcherNodes);
        }

        public RuleEvaluation Evaluate(string content)
        {
            var matches = Match(content);
            return MapEvaluation(matches);
        }

        private WordRule[] Match(string content)
        {
            var normalizedContent = Normalize(content);
            var matchedIndexes = new HashSet<int>();
            var state = 0;
            foreach (var character in normalizedContent)
            {
                while (state != 0 && !nodes[state].Transitions.ContainsKey(character))
                {
                    state = nodes[state].Failure;
                }

                if (nodes[state].Transitions.TryGetValue(character, out var nextState))
                {
                    state = nextState;
                }

                foreach (var output in nodes[state].Outputs)
                {
                    matchedIndexes.Add(output);
                }
            }

            return matchedIndexes.Select(index => rules[index]).ToArray();
        }

        private static void BuildFailureLinks(IReadOnlyList<MatcherNode> matcherNodes)
        {
            var queue = new Queue<int>();
            foreach (var rootChild in matcherNodes[0].Transitions.Values)
            {
                queue.Enqueue(rootChild);
            }

            while (queue.TryDequeue(out var state))
            {
                foreach (var transition in matcherNodes[state].Transitions)
                {
                    var character = transition.Key;
                    var nextState = transition.Value;
                    queue.Enqueue(nextState);

                    var failure = matcherNodes[state].Failure;
                    while (failure != 0 && !matcherNodes[failure].Transitions.ContainsKey(character))
                    {
                        failure = matcherNodes[failure].Failure;
                    }

                    if (matcherNodes[failure].Transitions.TryGetValue(character, out var fallback) &&
                        fallback != nextState)
                    {
                        matcherNodes[nextState].Failure = fallback;
                    }

                    matcherNodes[nextState].Outputs.AddRange(
                        matcherNodes[matcherNodes[nextState].Failure].Outputs);
                }
            }
        }

        private static RuleEvaluation MapEvaluation(IReadOnlyList<WordRule> matches)
        {
            var blackMatches = matches.Where(rule => rule.Type == WordRuleType.Black).ToArray();
            var suspiciousMatches = matches.Where(rule => rule.Type == WordRuleType.Suspicious).ToArray();
            var whiteMatches = matches.Where(rule => rule.Type == WordRuleType.White).ToArray();

            if (blackMatches.Length > 0)
            {
                return new RuleEvaluation(
                    ModerationDecision.Reject,
                    false,
                    null,
                    false,
                    0.99m,
                    "deterministic_rule",
                    "local_rules",
                    ["RULE_BLACK_WORD"],
                    blackMatches.Select(rule => new RuleCategory(rule.Category, 0.99m)).Distinct().ToArray(),
                    []);
            }

            var hasRelatedWhiteMatch = whiteMatches.Any(whiteRule =>
                suspiciousMatches.Any(suspiciousRule =>
                    string.Equals(whiteRule.Category, suspiciousRule.Category, StringComparison.Ordinal)));

            if (suspiciousMatches.Length > 0 && !hasRelatedWhiteMatch)
            {
                return new RuleEvaluation(
                    ModerationDecision.Review,
                    true,
                    "policy_required",
                    false,
                    null,
                    null,
                    "local_rules",
                    ["RULE_SUSPICIOUS_WORD", "CALLER_REVIEW_REQUIRED"],
                    suspiciousMatches.Select(rule => new RuleCategory(rule.Category, null)).Distinct().ToArray(),
                    []);
            }

            if (hasRelatedWhiteMatch || whiteMatches.Length > 0)
            {
                return new RuleEvaluation(
                    ModerationDecision.Review,
                    true,
                    "policy_required",
                    false,
                    null,
                    null,
                    "local_rules",
                    hasRelatedWhiteMatch
                        ? ["RULE_CONTEXT_EXCEPTION", "CALLER_REVIEW_REQUIRED"]
                        : ["RULE_WHITE_SIGNAL", "CALLER_REVIEW_REQUIRED"],
                    [],
                    []);
            }

            return new RuleEvaluation(
                ModerationDecision.Review,
                true,
                "policy_required",
                false,
                null,
                null,
                "local_rules",
                ["AI_ROUTE_NOT_CONFIGURED", "CALLER_REVIEW_REQUIRED"],
                [],
                []);
        }
    }

    private static string Normalize(string value)
    {
        return value.Normalize(NormalizationForm.FormKC).ToUpper(CultureInfo.InvariantCulture);
    }

    private sealed class MatcherNode
    {
        public Dictionary<char, int> Transitions { get; } = [];

        public List<int> Outputs { get; } = [];

        public int Failure { get; set; }
    }
}
