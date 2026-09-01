using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using VeriScan.Application.Contracts;
using VeriScan.Domain.Entities;

namespace VeriScan.Application.Services;

public static class RuleSetPolicyValidator
{
    public static RuleSetValidationResponse Validate(RuleSetVersion ruleSet)
    {
        var issues = new List<RuleSetValidationIssue>();
        var seen = new Dictionary<string, (WordRuleType Type, int Index)>(StringComparer.Ordinal);
        var index = 0;
        foreach (var rule in ruleSet.Rules.OrderBy(rule => rule.CreatedAt).ThenBy(rule => rule.Id))
        {
            var normalizedTerm = NormalizeTerm(rule.Term);
            if (normalizedTerm.Length == 0)
            {
                issues.Add(new RuleSetValidationIssue("EMPTY_TERM", "规范化后的词条不能为空。", index));
            }

            if (!IsCategoryCode(rule.Category))
            {
                issues.Add(new RuleSetValidationIssue(
                    "INVALID_CATEGORY",
                    "分类只能使用小写字母、数字、点、下划线或连字符。",
                    index));
            }

            if (rule.Weight is < 0 or > 1)
            {
                issues.Add(new RuleSetValidationIssue("INVALID_WEIGHT", "规则权重必须在 0 到 1 之间。", index));
            }

            var identity = $"{normalizedTerm}\0{rule.Category}";
            if (seen.TryGetValue(identity, out var existing))
            {
                issues.Add(new RuleSetValidationIssue(
                    existing.Type == rule.Type ? "DUPLICATE_RULE" : "CONFLICTING_RULE",
                    existing.Type == rule.Type
                        ? "同一分类中存在重复词条。"
                        : "同一分类中的词条不能同时使用冲突类型。",
                    index));
            }
            else
            {
                seen.Add(identity, (rule.Type, index));
            }

            index++;
        }

        if (ruleSet.Rules.Count == 0)
        {
            issues.Add(new RuleSetValidationIssue("EMPTY_RULE_SET", "规则集至少需要一条规则。", null));
        }

        var checksum = ComputeChecksum(ruleSet.Name, ruleSet.Rules);
        return new RuleSetValidationResponse(issues.Count == 0, checksum, ruleSet.Rules.Count, issues);
    }

    public static string ComputeChecksum(string name, IEnumerable<WordRule> rules)
    {
        var canonical = new StringBuilder();
        Append(canonical, name.Trim());
        foreach (var rule in rules
                     .OrderBy(rule => NormalizeTerm(rule.Term), StringComparer.Ordinal)
                     .ThenBy(rule => rule.Category, StringComparer.Ordinal)
                     .ThenBy(rule => rule.Type)
                     .ThenBy(rule => rule.Weight))
        {
            Append(canonical, NormalizeTerm(rule.Term));
            Append(canonical, rule.Type.ToString());
            Append(canonical, rule.Category);
            Append(canonical, rule.Weight.ToString("0.####", CultureInfo.InvariantCulture));
            Append(canonical, rule.IsEnabled ? "1" : "0");
        }

        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(canonical.ToString())));
    }

    private static string NormalizeTerm(string term)
    {
        return term.Trim().Normalize(NormalizationForm.FormKC).ToUpperInvariant();
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
