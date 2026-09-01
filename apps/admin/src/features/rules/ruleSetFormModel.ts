import type {
  RuleSet,
  RuleSetDraftInput,
  WordRuleDraftInput,
  WordRuleType,
} from '@/shared/api/types';

const typeAliases: Record<string, WordRuleType> = {
  black: 'black',
  reject: 'black',
  黑词: 'black',
  suspicious: 'suspicious',
  review: 'suspicious',
  可疑词: 'suspicious',
  white: 'white',
  pass: 'white',
  白词: 'white',
};

export interface ParsedRuleLines {
  rules: WordRuleDraftInput[];
  errors: string[];
}

export function parseRuleLines(source: string): ParsedRuleLines {
  const rules: WordRuleDraftInput[] = [];
  const errors: string[] = [];
  source.split(/\r?\n/).forEach((rawLine, index) => {
    const line = rawLine.trim();
    if (!line || line.startsWith('#')) return;
    const delimiter = line.includes('\t') ? '\t' : '|';
    const parts = line.split(delimiter).map((part) => part.trim());
    if (parts.length !== 4) {
      errors.push(`第 ${index + 1} 行需要 4 列：类型、分类、权重、词条`);
      return;
    }

    const type = typeAliases[parts[0].toLowerCase()];
    const weight = Number(parts[2]);
    if (!type) errors.push(`第 ${index + 1} 行的类型无效`);
    if (!/^[a-z0-9][a-z0-9._-]{0,63}$/.test(parts[1])) {
      errors.push(`第 ${index + 1} 行的分类代码无效`);
    }
    if (!Number.isFinite(weight) || weight < 0 || weight > 1) {
      errors.push(`第 ${index + 1} 行的权重必须在 0 到 1 之间`);
    }
    if (!parts[3]) errors.push(`第 ${index + 1} 行的词条不能为空`);
    if (type && parts[3] && Number.isFinite(weight) && weight >= 0 && weight <= 1) {
      rules.push({ type, category: parts[1], weight, term: parts[3] });
    }
  });
  if (rules.length === 0) errors.push('至少填写一条有效规则');
  return { rules, errors };
}

export function serializeRuleLines(ruleSet?: RuleSet | null): string {
  if (!ruleSet) {
    return ['# 类型 | 分类 | 权重 | 词条', 'black | gambling | 1 | 示例禁词'].join('\n');
  }
  return ruleSet.rules
    .map((rule) => `${rule.type} | ${rule.category} | ${rule.weight} | ${rule.term}`)
    .join('\n');
}

export function createRuleSetDraft(
  name: string,
  source: string,
): { value?: RuleSetDraftInput; errors: string[] } {
  const normalizedName = name.trim();
  const parsed = parseRuleLines(source);
  const errors = [...parsed.errors];
  if (normalizedName.length < 2 || normalizedName.length > 100) {
    errors.unshift('规则集名称长度必须在 2 到 100 个字符之间');
  }
  return errors.length > 0
    ? { errors }
    : { value: { name: normalizedName, rules: parsed.rules }, errors: [] };
}
