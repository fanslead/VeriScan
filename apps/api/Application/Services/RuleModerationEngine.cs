using System.Collections.Concurrent;
using System.Globalization;
using System.Text;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public interface IRuleModerationEngine
{
    RuleEvaluation Evaluate(string content, IReadOnlyList<WordRule> rules);

    RuleEvaluation Evaluate(
        string content,
        IReadOnlyList<WordRule> rules,
        IReadOnlyList<RegexRule> regexRules,
        IReadOnlyList<CombinationRule> combinationRules,
        RuleNormalizationOptions? normalizationOptions = null,
        string? language = null,
        string? scene = null);

    ICompiledRulePolicy GetOrCompile(string revisionId, IReadOnlyList<WordRule> rules);

    ICompiledRulePolicy GetOrCompile(
        string revisionId,
        IReadOnlyList<WordRule> rules,
        IReadOnlyList<RegexRule> regexRules,
        IReadOnlyList<CombinationRule> combinationRules,
        RuleNormalizationOptions? normalizationOptions = null);
}

public interface ICompiledRulePolicy
{
    RuleEvaluation Evaluate(string content, string? language = null, string? scene = null);
}

/// <summary>文本规范化配置。繁简映射必须显式启用，避免改变既有规则语义。</summary>
public sealed record RuleNormalizationOptions
{
    public RuleNormalizationProfile Profile { get; init; } = RuleNormalizationProfile.Default;

    public bool RemoveWhitespace { get; init; } = true;

    public bool RemoveZeroWidthCharacters { get; init; } = true;

    public bool RemoveControlCharacters { get; init; } = true;

    public IReadOnlyDictionary<char, char> CharacterMap { get; init; } =
        new Dictionary<char, char>();

    public static RuleNormalizationOptions ForProfile(RuleNormalizationProfile profile)
    {
        return new RuleNormalizationOptions
        {
            Profile = profile,
            CharacterMap = profile == RuleNormalizationProfile.TraditionalSimplified
                ? TraditionalSimplifiedMap
                : new Dictionary<char, char>()
        };
    }

    private static IReadOnlyDictionary<char, char> TraditionalSimplifiedMap { get; } =
        new Dictionary<char, char>
        {
            ['體'] = '体',
            ['臺'] = '台',
            ['國'] = '国',
            ['門'] = '门',
            ['後'] = '后',
            ['發'] = '发',
            ['現'] = '现',
            ['聯'] = '联',
            ['係'] = '系',
            ['類'] = '类',
            ['廣'] = '广',
            ['專'] = '专',
            ['業'] = '业',
            ['認'] = '认',
            ['證'] = '证',
            ['訊'] = '讯',
            ['會'] = '会',
            ['話'] = '话',
            ['學'] = '学',
            ['時'] = '时',
            ['間'] = '间',
            ['種'] = '种',
            ['為'] = '为',
            ['這'] = '这',
            ['與'] = '与',
            ['無'] = '无',
            ['點'] = '点',
            ['請'] = '请',
            ['來'] = '来',
            ['開'] = '开',
            ['關'] = '关',
            ['進'] = '进',
            ['還'] = '还',
            ['過'] = '过',
            ['將'] = '将',
            ['應'] = '应',
            ['華'] = '华',
            ['車'] = '车',
            ['電'] = '电',
            ['腦'] = '脑',
            ['網'] = '网',
            ['頁'] = '页',
            ['線'] = '线',
            ['買'] = '买',
            ['賣'] = '卖',
            ['貨'] = '货',
            ['錢'] = '钱',
            ['風'] = '风',
            ['頭'] = '头',
            ['長'] = '长',
            ['東'] = '东',
            ['書'] = '书',
            ['見'] = '见',
            ['實'] = '实',
            ['動'] = '动',
            ['對'] = '对',
            ['從'] = '从',
            ['個'] = '个',
            ['兩'] = '两',
            ['內'] = '内',
            ['並'] = '并',
            ['準'] = '准',
            ['則'] = '则',
            ['別'] = '别',
            ['區'] = '区',
            ['醫'] = '医',
            ['協'] = '协',
            ['單'] = '单',
            ['壓'] = '压',
            ['處'] = '处',
            ['備'] = '备',
            ['復'] = '复',
            ['選'] = '选',
            ['優'] = '优',
            ['價'] = '价',
            ['儲'] = '储',
            ['傳'] = '传',
            ['導'] = '导',
            ['檢'] = '检',
            ['測'] = '测',
            ['審'] = '审',
            ['標'] = '标',
            ['規'] = '规',
            ['庫'] = '库',
            ['詞'] = '词',
            ['據'] = '据'
        };
}

/// <summary>规范化后字符与原文位置之间的映射。</summary>
public sealed record NormalizedText(
    string Value,
    IReadOnlyList<NormalizedCharacterSpan> Spans);

/// <summary>一个规范化字符对应的原文 UTF-16 范围。</summary>
public readonly record struct NormalizedCharacterSpan(int OriginalStart, int OriginalLength);

/// <summary>规则命中的可审计证据。</summary>
public sealed record RuleEvidence(
    string RuleId,
    string RuleKind,
    string Category,
    RuleAction Action,
    string Quote,
    int OriginalStart,
    int OriginalLength,
    int NormalizedStart,
    int NormalizedLength,
    string? EvidenceTemplate = null);

/// <summary>规则命中结果。</summary>
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
    IReadOnlyList<string> Evidence)
{
    /// <summary>包含原文位置的结构化规则证据。</summary>
    public IReadOnlyList<RuleEvidence> EvidenceDetails { get; init; } = [];
}

public sealed record RuleCategory(string Code, decimal? RiskScore);

/// <summary>安全正则校验结果。</summary>
public sealed record RegexSafetyValidation(
    bool Valid,
    string? Code,
    string? Message);

/// <summary>动态正则的发布前安全检查。</summary>
public static class RegexRuleSafetyValidator
{
    public const int MaximumPatternLength = 2_048;
    public const int MaximumInputLength = 65_536;
    public const int MaximumCaptureGroups = 128;
    public const int MaximumTimeoutMs = 2_000;

    public static RegexSafetyValidation Validate(RegexRule rule)
    {
        ArgumentNullException.ThrowIfNull(rule);
        return Validate(rule.Pattern, rule.EngineMode, rule.TimeoutMs, rule.MaxInputLength);
    }

    public static RegexSafetyValidation Validate(
        string pattern,
        RegexRuleEngineMode engineMode = RegexRuleEngineMode.NonBacktracking,
        int timeoutMs = 100,
        int maxInputLength = MaximumInputLength)
    {
        if (string.IsNullOrWhiteSpace(pattern))
        {
            return Invalid("EMPTY_PATTERN", "正则表达式不能为空。");
        }

        if (pattern.Length > MaximumPatternLength)
        {
            return Invalid("PATTERN_TOO_LARGE", "正则表达式长度不能超过 2048 个字符。");
        }

        if (timeoutMs is < 1 or > MaximumTimeoutMs)
        {
            return Invalid("INVALID_REGEX_TIMEOUT", "正则匹配超时时间必须在 1 到 2000 毫秒之间。");
        }

        if (maxInputLength is < 1 or > MaximumInputLength)
        {
            return Invalid("INVALID_REGEX_INPUT_LIMIT", "正则输入长度必须在 1 到 65536 个字符之间。");
        }

        if (!Enum.IsDefined(engineMode))
        {
            return Invalid("INVALID_REGEX_ENGINE", "正则执行引擎不受支持。");
        }

        if (ContainsUnsupportedOrDangerousConstruct(pattern, out var code, out var message))
        {
            return Invalid(code, message);
        }

        try
        {
            _ = new System.Text.RegularExpressions.Regex(
                pattern,
                RegexRuleCompiler.GetOptions(engineMode),
                TimeSpan.FromMilliseconds(timeoutMs));
            return new RegexSafetyValidation(true, null, null);
        }
        catch (System.Text.RegularExpressions.RegexParseException)
        {
            return Invalid("INVALID_REGEX_SYNTAX", "正则表达式语法无效。");
        }
        catch (ArgumentException)
        {
            return Invalid("INVALID_REGEX_OPTIONS", "正则表达式不支持当前安全执行模式。");
        }
    }

    private static bool ContainsUnsupportedOrDangerousConstruct(
        string pattern,
        out string code,
        out string message)
    {
        var groupQuantifiers = new Stack<bool>();
        var inCharacterClass = false;
        var escaped = false;
        var captureGroups = 0;
        var previousWasQuantifier = false;

        for (var index = 0; index < pattern.Length; index++)
        {
            var character = pattern[index];
            if (escaped)
            {
                if (character is >= '1' and <= '9')
                {
                    code = "REGEX_BACKREFERENCE";
                    message = "正则表达式不允许使用反向引用。";
                    return true;
                }

                escaped = false;
                previousWasQuantifier = false;
                continue;
            }

            if (character == '\\')
            {
                escaped = true;
                continue;
            }

            if (character == '[')
            {
                inCharacterClass = true;
                previousWasQuantifier = false;
                continue;
            }

            if (character == ']' && inCharacterClass)
            {
                inCharacterClass = false;
                previousWasQuantifier = false;
                continue;
            }

            if (inCharacterClass)
            {
                continue;
            }

            if (character == '(')
            {
                if (index + 2 < pattern.Length && pattern[index + 1] == '?' &&
                    pattern[index + 2] is '=' or '!' or '<')
                {
                    code = "REGEX_LOOKAROUND";
                    message = "正则表达式不允许使用前瞻、后瞻等回溯相关语法。";
                    return true;
                }

                if (index + 1 < pattern.Length && pattern[index + 1] == '?')
                {
                    if (index + 2 >= pattern.Length ||
                        pattern[index + 2] is not ':' and not 'i' and not 'm' and not 's' and not '-')
                    {
                        code = "REGEX_UNSUPPORTED_GROUP";
                        message = "正则表达式包含不受支持的分组语法。";
                        return true;
                    }
                }
                else
                {
                    captureGroups++;
                    if (captureGroups > MaximumCaptureGroups)
                    {
                        code = "REGEX_TOO_MANY_GROUPS";
                        message = "正则表达式捕获组数量不能超过 128 个。";
                        return true;
                    }
                }

                groupQuantifiers.Push(false);
                previousWasQuantifier = false;
                continue;
            }

            if (character == ')')
            {
                if (groupQuantifiers.Count == 0)
                {
                    code = "INVALID_REGEX_SYNTAX";
                    message = "正则表达式分组括号不匹配。";
                    return true;
                }

                var containsQuantifier = groupQuantifiers.Pop();
                var nextIsQuantifier = index + 1 < pattern.Length && IsQuantifierStart(pattern[index + 1]);
                if (containsQuantifier && nextIsQuantifier)
                {
                    code = "REGEX_NESTED_QUANTIFIER";
                    message = "正则表达式包含可能导致回溯爆炸的嵌套量词。";
                    return true;
                }

                previousWasQuantifier = false;
                continue;
            }

            if (IsQuantifierStart(character))
            {
                if (previousWasQuantifier)
                {
                    code = "REGEX_NESTED_QUANTIFIER";
                    message = "正则表达式包含连续量词。";
                    return true;
                }

                if (groupQuantifiers.Count > 0)
                {
                    groupQuantifiers.Pop();
                    groupQuantifiers.Push(true);
                }

                previousWasQuantifier = true;
                continue;
            }

            previousWasQuantifier = false;
        }

        if (groupQuantifiers.Count != 0)
        {
            code = "INVALID_REGEX_SYNTAX";
            message = "正则表达式分组括号不匹配。";
            return true;
        }

        code = string.Empty;
        message = string.Empty;
        return false;
    }

    private static bool IsQuantifierStart(char character)
    {
        return character is '*' or '+' or '?' or '{';
    }

    private static RegexSafetyValidation Invalid(string code, string message)
    {
        return new RegexSafetyValidation(false, code, message);
    }
}

/// <summary>负责动态正则的受限编译。</summary>
public static class RegexRuleCompiler
{
    public static System.Text.RegularExpressions.RegexOptions GetOptions(RegexRuleEngineMode engineMode)
    {
        return System.Text.RegularExpressions.RegexOptions.CultureInvariant |
            System.Text.RegularExpressions.RegexOptions.IgnoreCase |
            (engineMode == RegexRuleEngineMode.NonBacktracking
                ? System.Text.RegularExpressions.RegexOptions.NonBacktracking
                : System.Text.RegularExpressions.RegexOptions.None);
    }

    public static bool TryCompile(
        RegexRule rule,
        out System.Text.RegularExpressions.Regex? regex,
        out RegexSafetyValidation validation)
    {
        validation = RegexRuleSafetyValidator.Validate(rule);
        if (!validation.Valid)
        {
            regex = null;
            return false;
        }

        try
        {
            regex = new System.Text.RegularExpressions.Regex(
                rule.Pattern,
                GetOptions(rule.EngineMode),
                TimeSpan.FromMilliseconds(rule.TimeoutMs));
            return true;
        }
        catch (System.Text.RegularExpressions.RegexParseException)
        {
            validation = new RegexSafetyValidation(false, "INVALID_REGEX_SYNTAX", "正则表达式语法无效。");
        }
        catch (ArgumentException)
        {
            validation = new RegexSafetyValidation(false, "INVALID_REGEX_OPTIONS", "正则表达式不支持当前安全执行模式。");
        }

        regex = null;
        return false;
    }
}

/// <summary>多级规则匹配与证据生成引擎。</summary>
public sealed class RuleModerationEngine : IRuleModerationEngine
{
    private const int MaximumCachedPolicies = 64;
    private const int MaximumRegexMatchesPerRule = 64;
    private readonly ConcurrentDictionary<string, Lazy<CompiledRulePolicy>> policies =
        new(StringComparer.Ordinal);
    private readonly ConcurrentQueue<string> insertionOrder = new();

    public RuleEvaluation Evaluate(string content, IReadOnlyList<WordRule> rules)
    {
        return Evaluate(content, rules, [], [], null);
    }

    public RuleEvaluation Evaluate(
        string content,
        IReadOnlyList<WordRule> rules,
        IReadOnlyList<RegexRule> regexRules,
        IReadOnlyList<CombinationRule> combinationRules,
        RuleNormalizationOptions? normalizationOptions = null,
        string? language = null,
        string? scene = null)
    {
        ArgumentNullException.ThrowIfNull(content);
        return CompiledRulePolicy.Create(
                rules,
                regexRules,
                combinationRules,
                normalizationOptions ?? RuleNormalizationOptions.ForProfile(RuleNormalizationProfile.Default))
            .Evaluate(content, language, scene);
    }

    public ICompiledRulePolicy GetOrCompile(string revisionId, IReadOnlyList<WordRule> rules)
    {
        return GetOrCompile(revisionId, rules, [], [], null);
    }

    public ICompiledRulePolicy GetOrCompile(
        string revisionId,
        IReadOnlyList<WordRule> rules,
        IReadOnlyList<RegexRule> regexRules,
        IReadOnlyList<CombinationRule> combinationRules,
        RuleNormalizationOptions? normalizationOptions = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(revisionId);
        var options = normalizationOptions ?? RuleNormalizationOptions.ForProfile(RuleNormalizationProfile.Default);
        var candidate = new Lazy<CompiledRulePolicy>(
            () => CompiledRulePolicy.Create(rules, regexRules, combinationRules, options),
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
        WordPattern[] wordPatterns,
        CompiledRegexRule[] regexRules,
        CombinationPattern[] combinationRules,
        MatcherNode[] nodes,
        RuleNormalizationOptions normalizationOptions,
        IReadOnlyList<string> compileWarnings) : ICompiledRulePolicy
    {
        public static CompiledRulePolicy Create(
            IReadOnlyList<WordRule> sourceRules,
            IReadOnlyList<RegexRule> sourceRegexRules,
            IReadOnlyList<CombinationRule> sourceCombinationRules,
            RuleNormalizationOptions normalizationOptions)
        {
            var wordPatterns = sourceRules
                .Where(rule => rule.IsEnabled)
                .Select(rule => new WordPattern(
                    rule,
                    RuleTextNormalizer.NormalizeValue(rule.Term, normalizationOptions)))
                .Where(pattern => pattern.Term.Length > 0)
                .OrderByDescending(pattern => pattern.Rule.Priority)
                .ThenBy(pattern => pattern.Rule.CreatedAt)
                .ToArray();
            var matcherNodes = new List<MatcherNode> { new() };
            for (var patternIndex = 0; patternIndex < wordPatterns.Length; patternIndex++)
            {
                var state = 0;
                foreach (var character in wordPatterns[patternIndex].Term)
                {
                    if (!matcherNodes[state].Transitions.TryGetValue(character, out var nextState))
                    {
                        nextState = matcherNodes.Count;
                        matcherNodes[state].Transitions.Add(character, nextState);
                        matcherNodes.Add(new MatcherNode());
                    }

                    state = nextState;
                }

                matcherNodes[state].Outputs.Add(new WordOutput(patternIndex, wordPatterns[patternIndex].Term.Length));
            }

            BuildFailureLinks(matcherNodes);

            var compileWarnings = new List<string>();
            var compiledRegexRules = sourceRegexRules
                .Where(rule => rule.IsEnabled)
                .Select(rule =>
                {
                    if (RegexRuleCompiler.TryCompile(rule, out var regex, out var validation))
                    {
                        return new CompiledRegexRule(rule, regex!);
                    }

                    if (validation.Code is not null)
                    {
                        compileWarnings.Add($"{validation.Code}:{rule.Id:N}");
                    }

                    return null;
                })
                .Where(rule => rule is not null)
                .Cast<CompiledRegexRule>()
                .OrderByDescending(rule => rule.Rule.Priority)
                .ThenBy(rule => rule.Rule.CreatedAt)
                .ToArray();

            var combinationRules = sourceCombinationRules
                .Where(rule => rule.IsEnabled)
                .Select(rule => new CombinationPattern(
                    rule,
                    rule.Terms
                        .Select(term => RuleTextNormalizer.NormalizeValue(term, normalizationOptions))
                        .Where(term => term.Length > 0)
                        .Distinct(StringComparer.Ordinal)
                        .ToArray()))
                .Where(pattern => pattern.Terms.Length > 1)
                .OrderByDescending(pattern => pattern.Rule.Priority)
                .ThenBy(pattern => pattern.Rule.CreatedAt)
                .ToArray();

            return new CompiledRulePolicy(
                wordPatterns,
                compiledRegexRules,
                combinationRules,
                matcherNodes.ToArray(),
                normalizationOptions,
                compileWarnings);
        }

        public RuleEvaluation Evaluate(string content, string? language = null, string? scene = null)
        {
            ArgumentNullException.ThrowIfNull(content);
            var normalized = RuleTextNormalizer.Normalize(content, normalizationOptions);
            var matches = new List<RuleMatch>();
            MatchWords(normalized, matches, language, scene);
            MatchRegex(normalized, matches, language, scene);
            MatchCombinations(normalized, matches, language, scene);
            return MapEvaluation(content, normalized, matches, compileWarnings);
        }

        private void MatchWords(
            NormalizedText normalized,
            ICollection<RuleMatch> matches,
            string? language,
            string? scene)
        {
            var state = 0;
            for (var index = 0; index < normalized.Value.Length; index++)
            {
                var character = normalized.Value[index];
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
                    var start = index - output.Length + 1;
                    if (start >= 0 && IsApplicable(wordPatterns[output.PatternIndex].Rule, language, scene))
                    {
                        matches.Add(new RuleMatch(
                            wordPatterns[output.PatternIndex].Rule,
                            "word",
                            start,
                            output.Length));
                    }
                }
            }
        }

        private void MatchRegex(
            NormalizedText normalized,
            ICollection<RuleMatch> matches,
            string? language,
            string? scene)
        {
            foreach (var compiledRule in regexRules)
            {
                if (!IsApplicable(compiledRule.Rule, language, scene) ||
                    normalized.Value.Length > compiledRule.Rule.MaxInputLength)
                {
                    continue;
                }

                try
                {
                    var count = 0;
                    foreach (System.Text.RegularExpressions.Match match in compiledRule.Regex.Matches(normalized.Value))
                    {
                        if (match.Success && match.Length > 0)
                        {
                            matches.Add(new RuleMatch(
                                compiledRule.Rule,
                                "regex",
                                match.Index,
                                match.Length));
                            count++;
                            if (count >= MaximumRegexMatchesPerRule)
                            {
                                break;
                            }
                        }
                    }
                }
                catch (System.Text.RegularExpressions.RegexMatchTimeoutException)
                {
                    matches.Add(RuleMatch.Warning(compiledRule.Rule, "REGEX_MATCH_TIMEOUT"));
                }
            }
        }

        private void MatchCombinations(
            NormalizedText normalized,
            ICollection<RuleMatch> matches,
            string? language,
            string? scene)
        {
            foreach (var combination in combinationRules)
            {
                if (!IsApplicable(combination.Rule, language, scene))
                {
                    continue;
                }

                var occurrences = new List<CombinationOccurrence>();
                for (var termIndex = 0; termIndex < combination.Terms.Length; termIndex++)
                {
                    var term = combination.Terms[termIndex];
                    var searchStart = 0;
                    var found = false;
                    while (searchStart < normalized.Value.Length)
                    {
                        var index = normalized.Value.IndexOf(term, searchStart, StringComparison.Ordinal);
                        if (index < 0)
                        {
                            break;
                        }

                        occurrences.Add(new CombinationOccurrence(termIndex, index, term.Length));
                        found = true;
                        searchStart = index + Math.Max(1, term.Length);
                        if (occurrences.Count > 4_096)
                        {
                            break;
                        }
                    }

                    if (!found)
                    {
                        occurrences.Clear();
                        break;
                    }
                }

                if (occurrences.Count == 0)
                {
                    continue;
                }

                occurrences.Sort(static (left, right) => left.Start.CompareTo(right.Start));
                var termCounts = new int[combination.Terms.Length];
                var distinctTerms = 0;
                var leftIndex = 0;
                for (var rightIndex = 0; rightIndex < occurrences.Count; rightIndex++)
                {
                    var right = occurrences[rightIndex];
                    if (termCounts[right.TermIndex]++ == 0)
                    {
                        distinctTerms++;
                    }

                    while (distinctTerms == combination.Terms.Length && leftIndex <= rightIndex)
                    {
                        var left = occurrences[leftIndex];
                        var spanEnd = right.Start + right.Length;
                        var spanLength = spanEnd - left.Start;
                        if (spanLength <= combination.Rule.WindowSize)
                        {
                            matches.Add(new RuleMatch(
                                combination.Rule,
                                "combination",
                                left.Start,
                                spanLength));
                            break;
                        }

                        if (--termCounts[left.TermIndex] == 0)
                        {
                            distinctTerms--;
                        }

                        leftIndex++;
                    }

                    if (matches.Any(match => ReferenceEquals(match.Rule, combination.Rule)))
                    {
                        break;
                    }
                }
            }
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

        private static RuleEvaluation MapEvaluation(
            string content,
            NormalizedText normalized,
            IReadOnlyList<RuleMatch> matches,
            IReadOnlyList<string> compileWarnings)
        {
            var actualMatches = matches.Where(match => !match.IsWarning).ToArray();
            var evidence = actualMatches
                .Select(match => match.ToEvidence(content, normalized))
                .Where(item => item is not null)
                .Cast<RuleEvidence>()
                .DistinctBy(item => new
                {
                    item.RuleId,
                    item.OriginalStart,
                    item.OriginalLength
                })
                .OrderByDescending(item => item.Action == RuleAction.HardReject)
                .ThenBy(item => item.OriginalStart)
                .ToArray();
            var reasonCodes = new List<string>();
            if (compileWarnings.Count > 0)
            {
                reasonCodes.AddRange(compileWarnings.Select(warning => warning.Split(':')[0]));
            }

            reasonCodes.AddRange(
                matches
                    .Where(match => match.IsWarning && match.WarningCode is not null)
                    .Select(match => match.WarningCode!));

            var hardRejects = actualMatches
                .Where(match => GetAction(match.Rule) == RuleAction.HardReject)
                .ToArray();
            if (hardRejects.Length > 0)
            {
                reasonCodes.Add("RULE_HARD_REJECT");
                if (hardRejects.Any(match => match.Rule is WordRule { Type: WordRuleType.Black }))
                {
                    reasonCodes.Add("RULE_BLACK_WORD");
                }

                return CreateEvaluation(
                    ModerationDecision.Reject,
                    false,
                    null,
                    0.99m,
                    "deterministic_rule",
                    "local_rules",
                    reasonCodes,
                    hardRejects,
                    evidence);
            }

            var forceReviews = actualMatches
                .Where(match => GetAction(match.Rule) == RuleAction.ForceReview)
                .ToArray();
            if (forceReviews.Length > 0)
            {
                reasonCodes.Add("RULE_FORCE_REVIEW");
                reasonCodes.Add("CALLER_REVIEW_REQUIRED");
                return CreateEvaluation(
                    ModerationDecision.Review,
                    false,
                    "rule_force_review",
                    MaxWeight(forceReviews),
                    "deterministic_rule",
                    "local_rules",
                    reasonCodes,
                    forceReviews,
                    evidence);
            }

            var signals = actualMatches
                .Where(match => GetAction(match.Rule) == RuleAction.RiskSignal)
                .ToArray();
            var exceptions = actualMatches
                .Where(match => GetAction(match.Rule) == RuleAction.ContextException)
                .ToArray();
            var activeSignals = signals
                .Where(signal => !exceptions.Any(exception =>
                    string.Equals(GetCategory(exception.Rule), GetCategory(signal.Rule), StringComparison.Ordinal)))
                .ToArray();
            if (activeSignals.Length > 0)
            {
                reasonCodes.Add("RULE_RISK_SIGNAL");
                if (activeSignals.Any(match => match.Rule is WordRule { Type: WordRuleType.Suspicious }))
                {
                    reasonCodes.Add("RULE_SUSPICIOUS_WORD");
                }

                reasonCodes.Add("CALLER_REVIEW_REQUIRED");
                return CreateEvaluation(
                    ModerationDecision.Review,
                    true,
                    "policy_required",
                    null,
                    null,
                    "local_rules",
                    reasonCodes,
                    activeSignals,
                    evidence);
            }

            if (exceptions.Length > 0)
            {
                reasonCodes.Add("RULE_CONTEXT_EXCEPTION");
            }

            if (actualMatches.Any(match => GetAction(match.Rule) == RuleAction.MonitorOnly))
            {
                reasonCodes.Add("RULE_MONITOR_ONLY");
            }

            reasonCodes.Add("AI_ROUTE_NOT_CONFIGURED");
            reasonCodes.Add("CALLER_REVIEW_REQUIRED");
            return CreateEvaluation(
                ModerationDecision.Review,
                true,
                "policy_required",
                null,
                null,
                "local_rules",
                reasonCodes,
                actualMatches,
                evidence);
        }

        private static RuleEvaluation CreateEvaluation(
            ModerationDecision decision,
            bool requiresAi,
            string? reviewSource,
            decimal? riskScore,
            string? scoreSource,
            string route,
            IEnumerable<string> reasonCodes,
            IEnumerable<RuleMatch> categoryMatches,
            IReadOnlyList<RuleEvidence> evidence)
        {
            var categories = categoryMatches
                .Select(match => new RuleCategory(
                    GetCategory(match.Rule),
                    GetAction(match.Rule) == RuleAction.HardReject ? 0.99m : null))
                .Distinct()
                .ToArray();
            var evidenceTexts = evidence.Select(item => item.Quote).Distinct(StringComparer.Ordinal).ToArray();
            var result = new RuleEvaluation(
                decision,
                requiresAi,
                reviewSource,
                false,
                riskScore,
                scoreSource,
                route,
                reasonCodes.Distinct(StringComparer.Ordinal).ToArray(),
                categories,
                evidenceTexts);
            return result with { EvidenceDetails = evidence };
        }

        private static decimal MaxWeight(IEnumerable<RuleMatch> matches)
        {
            return matches.Select(match => GetWeight(match.Rule)).DefaultIfEmpty(0).Max();
        }

        private static bool IsApplicable(object rule, string? language, string? scene)
        {
            var ruleLanguage = rule switch
            {
                WordRule word => word.Language,
                RegexRule regex => regex.Language,
                CombinationRule combination => combination.Language,
                _ => null
            };
            var ruleScene = rule switch
            {
                WordRule word => word.Scene,
                RegexRule regex => regex.Scene,
                CombinationRule combination => combination.Scene,
                _ => null
            };
            return (ruleLanguage is null || language is null ||
                    string.Equals(language, ruleLanguage, StringComparison.OrdinalIgnoreCase) ||
                    language.StartsWith(ruleLanguage + "-", StringComparison.OrdinalIgnoreCase)) &&
                   (ruleScene is null || scene is null ||
                    string.Equals(scene, ruleScene, StringComparison.OrdinalIgnoreCase));
        }

        private static RuleAction GetAction(object rule)
        {
            return rule switch
            {
                WordRule word => word.Action,
                RegexRule regex => regex.Action,
                CombinationRule combination => combination.Action,
                _ => RuleAction.MonitorOnly
            };
        }

        private static string GetCategory(object rule)
        {
            return rule switch
            {
                WordRule word => word.Category,
                RegexRule regex => regex.Category,
                CombinationRule combination => combination.Category,
                _ => string.Empty
            };
        }

        private static decimal GetWeight(object rule)
        {
            return rule switch
            {
                WordRule word => word.Weight,
                RegexRule regex => regex.Weight,
                CombinationRule combination => combination.Weight,
                _ => 0
            };
        }
    }

    private sealed class MatcherNode
    {
        public Dictionary<char, int> Transitions { get; } = [];

        public List<WordOutput> Outputs { get; } = [];

        public int Failure { get; set; }
    }

    private sealed record WordPattern(WordRule Rule, string Term);

    private sealed record WordOutput(int PatternIndex, int Length);

    private sealed record CompiledRegexRule(RegexRule Rule, System.Text.RegularExpressions.Regex Regex);

    private sealed record CombinationPattern(CombinationRule Rule, string[] Terms);

    private sealed record CombinationOccurrence(int TermIndex, int Start, int Length);

    private sealed class RuleMatch
    {
        public RuleMatch(
            object rule,
            string kind,
            int normalizedStart,
            int normalizedLength,
            string? warningCode = null)
        {
            Rule = rule;
            Kind = kind;
            NormalizedStart = normalizedStart;
            NormalizedLength = normalizedLength;
            WarningCode = warningCode;
        }

        public object Rule { get; }

        public string Kind { get; }

        public int NormalizedStart { get; }

        public int NormalizedLength { get; }

        public string? WarningCode { get; }

        public bool IsWarning => WarningCode is not null;

        public static RuleMatch Warning(RegexRule rule, string warningCode)
        {
            return new RuleMatch(rule, "regex", 0, 0, warningCode);
        }

        public RuleEvidence? ToEvidence(string content, NormalizedText normalized)
        {
            if (IsWarning || NormalizedLength <= 0 ||
                NormalizedStart < 0 || NormalizedStart + NormalizedLength > normalized.Spans.Count)
            {
                return null;
            }

            var category = Rule switch
            {
                WordRule word => word.Category,
                RegexRule regex => regex.Category,
                CombinationRule combination => combination.Category,
                _ => string.Empty
            };
            var action = Rule switch
            {
                WordRule word => word.Action,
                RegexRule regex => regex.Action,
                CombinationRule combination => combination.Action,
                _ => RuleAction.MonitorOnly
            };
            var evidenceTemplate = Rule switch
            {
                WordRule word => word.EvidenceTemplate,
                RegexRule regex => regex.EvidenceTemplate,
                CombinationRule combination => combination.EvidenceTemplate,
                _ => null
            };
            var id = Rule switch
            {
                WordRule word => word.Id,
                RegexRule regex => regex.Id,
                CombinationRule combination => combination.Id,
                _ => Guid.Empty
            };
            var first = normalized.Spans[NormalizedStart];
            var last = normalized.Spans[NormalizedStart + NormalizedLength - 1];
            var originalStart = first.OriginalStart;
            var originalEnd = last.OriginalStart + last.OriginalLength;
            if (originalStart < 0 || originalEnd > content.Length || originalEnd <= originalStart)
            {
                return null;
            }

            var originalLength = originalEnd - originalStart;
            var quoteLength = Math.Min(originalLength, 256);
            return new RuleEvidence(
                id.ToString("N"),
                Kind,
                category,
                action,
                content.Substring(originalStart, quoteLength),
                originalStart,
                originalLength,
                NormalizedStart,
                NormalizedLength,
                evidenceTemplate);
        }
    }
}

/// <summary>对输入文本执行稳定的 Unicode 规范化并保留原文映射。</summary>
public static class RuleTextNormalizer
{
    private static readonly HashSet<char> ZeroWidthCharacters =
    [
        '\u200B', '\u200C', '\u200D', '\u2060', '\uFEFF', '\u180E'
    ];

    public static NormalizedText Normalize(
        string value,
        RuleNormalizationOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(value);
        options ??= RuleNormalizationOptions.ForProfile(RuleNormalizationProfile.Default);
        var globallyNormalized = value.Normalize(NormalizationForm.FormKC).ToUpperInvariant();
        if (globallyNormalized.Length == value.Length &&
            !globallyNormalized.Any(char.IsSurrogate) &&
            !globallyNormalized.Any(character => ShouldRemove(character, options)) &&
            !globallyNormalized.Any(character => options.CharacterMap.ContainsKey(character)))
        {
            var fastSpans = new NormalizedCharacterSpan[value.Length];
            for (var index = 0; index < fastSpans.Length; index++)
            {
                fastSpans[index] = new NormalizedCharacterSpan(index, 1);
            }

            return new NormalizedText(globallyNormalized, fastSpans);
        }

        var builder = new StringBuilder(value.Length);
        var spans = new List<NormalizedCharacterSpan>(value.Length);
        var sourceIndex = 0;
        foreach (var rune in value.EnumerateRunes())
        {
            var sourceLength = rune.Utf16SequenceLength;
            var normalized = rune.ToString().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
            foreach (var character in normalized)
            {
                if (ShouldRemove(character, options))
                {
                    continue;
                }

                var mapped = character;
                if (options.CharacterMap.TryGetValue(character, out var simplified))
                {
                    mapped = simplified;
                }

                builder.Append(mapped);
                spans.Add(new NormalizedCharacterSpan(sourceIndex, sourceLength));
            }

            sourceIndex += sourceLength;
        }

        return new NormalizedText(builder.ToString(), spans);
    }

    public static string NormalizeValue(
        string value,
        RuleNormalizationOptions? options = null)
    {
        return Normalize(value, options).Value;
    }

    private static bool ShouldRemove(char character, RuleNormalizationOptions options)
    {
        if (options.RemoveWhitespace && char.IsWhiteSpace(character))
        {
            return true;
        }

        if (options.RemoveZeroWidthCharacters && ZeroWidthCharacters.Contains(character))
        {
            return true;
        }

        if (options.RemoveControlCharacters &&
            (char.IsControl(character) || char.GetUnicodeCategory(character) == UnicodeCategory.Format))
        {
            return true;
        }

        return false;
    }
}
