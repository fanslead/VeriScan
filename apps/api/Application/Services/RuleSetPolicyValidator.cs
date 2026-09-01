using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

/// <summary>规则集发布前的确定性校验和规范化校验。</summary>
public static class RuleSetPolicyValidator
{
    public static RuleSetValidationResponse Validate(RuleSetVersion ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        var issues = new List<RuleSetValidationIssue>();
        if (!Enum.IsDefined(ruleSet.NormalizationProfile))
        {
            issues.Add(new RuleSetValidationIssue(
                "INVALID_NORMALIZATION_PROFILE",
                "文本规范化配置不受支持。",
                null));
        }

        var options = RuleNormalizationOptions.ForProfile(ruleSet.NormalizationProfile);
        var seenWords = new Dictionary<string, (WordRuleType Type, RuleAction Action, int Index)>(StringComparer.Ordinal);
        var ruleIndex = 0;

        foreach (var rule in ruleSet.Rules.OrderBy(rule => rule.CreatedAt).ThenBy(rule => rule.Id))
        {
            var normalizedTerm = RuleTextNormalizer.NormalizeValue(rule.Term, options);
            if (normalizedTerm.Length == 0)
            {
                issues.Add(new RuleSetValidationIssue("EMPTY_TERM", "规范化后的词条不能为空。", ruleIndex));
            }

            ValidateCategory(rule.Category, ruleIndex, issues);
            ValidateWeight(rule.Weight, ruleIndex, issues);
            ValidateOptionalText(rule.Language, 32, "INVALID_LANGUAGE", ruleIndex, issues);
            ValidateOptionalText(rule.Scene, 64, "INVALID_SCENE", ruleIndex, issues);
            ValidateOptionalText(rule.EvidenceTemplate, 256, "INVALID_EVIDENCE_TEMPLATE", ruleIndex, issues);
            ValidateAction(rule.Action, ruleIndex, issues);
            if (!Enum.IsDefined(rule.MatchMode))
            {
                issues.Add(new RuleSetValidationIssue("INVALID_MATCH_MODE", "词条匹配方式不受支持。", ruleIndex));
            }

            var identity = $"{normalizedTerm}\0{rule.Category.ToLowerInvariant()}";
            if (seenWords.TryGetValue(identity, out var existing))
            {
                issues.Add(new RuleSetValidationIssue(
                    existing.Type == rule.Type && existing.Action == rule.Action
                        ? "DUPLICATE_RULE"
                        : "CONFLICTING_RULE",
                    existing.Type == rule.Type && existing.Action == rule.Action
                        ? "同一分类中存在重复词条。"
                        : "同一分类中的词条不能同时使用冲突类型或动作。",
                    ruleIndex));
            }
            else
            {
                seenWords.Add(identity, (rule.Type, rule.Action, ruleIndex));
            }

            ruleIndex++;
        }

        var seenRegex = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in ruleSet.RegexRules.OrderBy(rule => rule.CreatedAt).ThenBy(rule => rule.Id))
        {
            var regexValidation = RegexRuleSafetyValidator.Validate(rule);
            if (!regexValidation.Valid)
            {
                issues.Add(new RuleSetValidationIssue(
                    regexValidation.Code ?? "INVALID_REGEX",
                    regexValidation.Message ?? "正则表达式校验失败。",
                    ruleIndex));
            }

            ValidateCategory(rule.Category, ruleIndex, issues);
            ValidateWeight(rule.Weight, ruleIndex, issues);
            ValidateOptionalText(rule.Language, 32, "INVALID_LANGUAGE", ruleIndex, issues);
            ValidateOptionalText(rule.Scene, 64, "INVALID_SCENE", ruleIndex, issues);
            ValidateOptionalText(rule.EvidenceTemplate, 256, "INVALID_EVIDENCE_TEMPLATE", ruleIndex, issues);
            ValidateAction(rule.Action, ruleIndex, issues);

            var identity = $"{rule.Pattern}\0{rule.Category.ToLowerInvariant()}\0{rule.Action}";
            if (!seenRegex.Add(identity))
            {
                issues.Add(new RuleSetValidationIssue(
                    "DUPLICATE_REGEX_RULE",
                    "同一分类和动作中存在重复正则规则。",
                    ruleIndex));
            }

            ruleIndex++;
        }

        var seenCombinations = new HashSet<string>(StringComparer.Ordinal);
        foreach (var rule in ruleSet.CombinationRules.OrderBy(rule => rule.CreatedAt).ThenBy(rule => rule.Id))
        {
            var normalizedTerms = rule.Terms
                .Select(term => RuleTextNormalizer.NormalizeValue(term, options))
                .ToArray();
            if (normalizedTerms.Length is < 2 or > 16)
            {
                issues.Add(new RuleSetValidationIssue(
                    "INVALID_COMBINATION_TERMS",
                    "组合规则必须包含 2 到 16 个不重复词条。",
                    ruleIndex));
            }

            if (normalizedTerms.Any(term => term.Length == 0) ||
                normalizedTerms.Distinct(StringComparer.Ordinal).Count() != normalizedTerms.Length)
            {
                issues.Add(new RuleSetValidationIssue(
                    "DUPLICATE_COMBINATION_TERM",
                    "组合规则中的词条不能为空且不能重复。",
                    ruleIndex));
            }

            if (rule.Name.Trim().Length is < 1 or > 128)
            {
                issues.Add(new RuleSetValidationIssue(
                    "INVALID_COMBINATION_NAME",
                    "组合规则名称长度必须在 1 到 128 个字符之间。",
                    ruleIndex));
            }

            if (rule.WindowSize is < 1 or > 4_096)
            {
                issues.Add(new RuleSetValidationIssue(
                    "INVALID_COMBINATION_WINDOW",
                    "组合规则窗口必须在 1 到 4096 个规范化字符之间。",
                    ruleIndex));
            }

            ValidateCategory(rule.Category, ruleIndex, issues);
            ValidateWeight(rule.Weight, ruleIndex, issues);
            ValidateOptionalText(rule.Language, 32, "INVALID_LANGUAGE", ruleIndex, issues);
            ValidateOptionalText(rule.Scene, 64, "INVALID_SCENE", ruleIndex, issues);
            ValidateOptionalText(rule.EvidenceTemplate, 256, "INVALID_EVIDENCE_TEMPLATE", ruleIndex, issues);
            ValidateAction(rule.Action, ruleIndex, issues);

            var identity = string.Join('\0', normalizedTerms.OrderBy(term => term, StringComparer.Ordinal)) +
                $"\0{rule.Category.ToLowerInvariant()}\0{rule.Action}\0{rule.WindowSize}";
            if (!seenCombinations.Add(identity))
            {
                issues.Add(new RuleSetValidationIssue(
                    "DUPLICATE_COMBINATION_RULE",
                    "组合规则的词条、分类、动作和窗口不能完全重复。",
                    ruleIndex));
            }

            ruleIndex++;
        }

        if (ruleIndex == 0)
        {
            issues.Add(new RuleSetValidationIssue("EMPTY_RULE_SET", "规则集至少需要一条规则。", null));
        }

        var checksum = ComputeChecksum(ruleSet);
        return new RuleSetValidationResponse(issues.Count == 0, checksum, ruleIndex, issues);
    }

    public static string ComputeChecksum(string name, IEnumerable<WordRule> rules)
    {
        return ComputeChecksum(
            name,
            RuleNormalizationProfile.Default,
            rules,
            [],
            []);
    }

    public static string ComputeChecksum(RuleSetVersion ruleSet)
    {
        ArgumentNullException.ThrowIfNull(ruleSet);
        return ComputeChecksum(
            ruleSet.Name,
            ruleSet.NormalizationProfile,
            ruleSet.Rules,
            ruleSet.RegexRules,
            ruleSet.CombinationRules);
    }

    public static string ComputeChecksum(
        string name,
        RuleNormalizationProfile normalizationProfile,
        IEnumerable<WordRule> rules,
        IEnumerable<RegexRule> regexRules,
        IEnumerable<CombinationRule> combinationRules)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(rules);
        ArgumentNullException.ThrowIfNull(regexRules);
        ArgumentNullException.ThrowIfNull(combinationRules);
        var options = RuleNormalizationOptions.ForProfile(normalizationProfile);
        var canonical = new StringBuilder();
        Append(canonical, name.Trim());
        Append(canonical, normalizationProfile.ToString());

        foreach (var rule in rules
                     .OrderBy(rule => RuleTextNormalizer.NormalizeValue(rule.Term, options), StringComparer.Ordinal)
                     .ThenBy(rule => rule.Category, StringComparer.Ordinal)
                     .ThenBy(rule => rule.Type)
                     .ThenBy(rule => rule.Action)
                     .ThenBy(rule => rule.Weight))
        {
            Append(canonical, "word");
            Append(canonical, RuleTextNormalizer.NormalizeValue(rule.Term, options));
            Append(canonical, rule.Type.ToString());
            Append(canonical, rule.Action.ToString());
            Append(canonical, rule.MatchMode.ToString());
            Append(canonical, rule.Category.Trim().ToLowerInvariant());
            Append(canonical, rule.Weight.ToString("0.####", CultureInfo.InvariantCulture));
            Append(canonical, rule.Language ?? string.Empty);
            Append(canonical, rule.Scene ?? string.Empty);
            Append(canonical, rule.EvidenceTemplate ?? string.Empty);
            Append(canonical, rule.Priority.ToString(CultureInfo.InvariantCulture));
            Append(canonical, rule.Source ?? string.Empty);
            Append(canonical, rule.IsEnabled ? "1" : "0");
        }

        foreach (var rule in regexRules
                     .OrderBy(rule => rule.Pattern, StringComparer.Ordinal)
                     .ThenBy(rule => rule.Category, StringComparer.Ordinal)
                     .ThenBy(rule => rule.Action)
                     .ThenBy(rule => rule.Weight))
        {
            Append(canonical, "regex");
            Append(canonical, rule.Pattern);
            Append(canonical, rule.Action.ToString());
            Append(canonical, rule.Category.Trim().ToLowerInvariant());
            Append(canonical, rule.Weight.ToString("0.####", CultureInfo.InvariantCulture));
            Append(canonical, rule.TimeoutMs.ToString(CultureInfo.InvariantCulture));
            Append(canonical, rule.MaxInputLength.ToString(CultureInfo.InvariantCulture));
            Append(canonical, rule.EngineMode.ToString());
            Append(canonical, rule.Language ?? string.Empty);
            Append(canonical, rule.Scene ?? string.Empty);
            Append(canonical, rule.EvidenceTemplate ?? string.Empty);
            Append(canonical, rule.Priority.ToString(CultureInfo.InvariantCulture));
            Append(canonical, rule.Source ?? string.Empty);
            Append(canonical, rule.IsEnabled ? "1" : "0");
        }

        foreach (var rule in combinationRules
                     .OrderBy(rule => string.Join('\0', rule.Terms.OrderBy(term => term, StringComparer.Ordinal)), StringComparer.Ordinal)
                     .ThenBy(rule => rule.Category, StringComparer.Ordinal)
                     .ThenBy(rule => rule.Action)
                     .ThenBy(rule => rule.Weight))
        {
            Append(canonical, "combination");
            Append(canonical, rule.Name.Trim());
            foreach (var term in rule.Terms
                         .Select(term => RuleTextNormalizer.NormalizeValue(term, options))
                         .OrderBy(term => term, StringComparer.Ordinal))
            {
                Append(canonical, term);
            }

            Append(canonical, rule.Action.ToString());
            Append(canonical, rule.Category.Trim().ToLowerInvariant());
            Append(canonical, rule.Weight.ToString("0.####", CultureInfo.InvariantCulture));
            Append(canonical, rule.WindowSize.ToString(CultureInfo.InvariantCulture));
            Append(canonical, rule.Language ?? string.Empty);
            Append(canonical, rule.Scene ?? string.Empty);
            Append(canonical, rule.EvidenceTemplate ?? string.Empty);
            Append(canonical, rule.Priority.ToString(CultureInfo.InvariantCulture));
            Append(canonical, rule.Source ?? string.Empty);
            Append(canonical, rule.IsEnabled ? "1" : "0");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static void ValidateCategory(
        string category,
        int index,
        List<RuleSetValidationIssue> issues)
    {
        if (!IsCategoryCode(category))
        {
            issues.Add(new RuleSetValidationIssue(
                "INVALID_CATEGORY",
                "分类只能使用字母、数字、点、下划线或连字符，且必须以字母或数字开头。",
                index));
        }
    }

    private static void ValidateWeight(
        decimal weight,
        int index,
        List<RuleSetValidationIssue> issues)
    {
        if (weight is < 0 or > 1)
        {
            issues.Add(new RuleSetValidationIssue("INVALID_WEIGHT", "规则权重必须在 0 到 1 之间。", index));
        }
    }

    private static void ValidateOptionalText(
        string? value,
        int maxLength,
        string code,
        int index,
        List<RuleSetValidationIssue> issues)
    {
        if (value is not null && value.Length > maxLength)
        {
            issues.Add(new RuleSetValidationIssue(code, "规则说明字段超过长度限制。", index));
        }
    }

    private static void ValidateAction(
        RuleAction action,
        int index,
        List<RuleSetValidationIssue> issues)
    {
        if (!Enum.IsDefined(action))
        {
            issues.Add(new RuleSetValidationIssue("INVALID_ACTION", "规则动作不受支持。", index));
        }
    }

    private static bool IsCategoryCode(string category)
    {
        return category.Length is > 0 and <= 64 &&
               char.IsAsciiLetterOrDigit(category[0]) &&
               category.All(character =>
                   char.IsAsciiLetterOrDigit(character) || character is '.' or '_' or '-');
    }

    private static void Append(StringBuilder target, string value)
    {
        target.Append(value.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(value);
    }
}
