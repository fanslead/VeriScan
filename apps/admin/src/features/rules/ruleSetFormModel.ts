import type {
  CombinationRuleDraftInput,
  RegexRuleDraftInput,
  RuleAction,
  RuleNormalizationProfile,
  RuleSet,
  RuleSetDraftInput,
  WordRuleDraftInput,
  WordRuleType,
} from '@/shared/api/types';

export const ruleActionOptions: Array<{
  value: WordRuleType;
  label: string;
  shortLabel: string;
  description: string;
  preview: string;
}> = [
  {
    value: 'black',
    label: '直接拦截',
    shortLabel: '拦截',
    description: '适合明确违规、无需结合上下文判断的词。',
    preview: '命中后直接判定为不通过',
  },
  {
    value: 'suspicious',
    label: '交给 AI 判断',
    shortLabel: 'AI 判断',
    description: '适合可能违规，但需要结合整段话判断的词。',
    preview: '命中后交给 AI 结合上下文判断',
  },
  {
    value: 'white',
    label: '作为语境例外',
    shortLabel: '语境例外',
    description: '只减弱同类可疑信号，不会让内容直接通过。',
    preview: '命中后减弱同分类的可疑信号',
  },
];

export const ruleCategoryOptions = [
  { value: 'fraud', label: '诈骗与欺诈' },
  { value: 'gambling', label: '赌博博彩' },
  { value: 'sexual', label: '色情低俗' },
  { value: 'violence', label: '暴恐与伤害' },
  { value: 'contact', label: '联系方式与导流' },
  { value: 'abuse', label: '辱骂与仇恨' },
  { value: 'illegal', label: '违法违规' },
  { value: 'product', label: '品牌与产品语境' },
];

export const suspiciousStrengthOptions = [
  { value: 0.4, label: '一般提醒', hint: '轻度相关时使用' },
  { value: 0.6, label: '重点关注', hint: '默认，较可能存在风险' },
  { value: 0.8, label: '高度可疑', hint: '强烈建议交给 AI 判断' },
];

export const universalRuleActionOptions: Array<{
  value: RuleAction;
  label: string;
  shortLabel: string;
  description: string;
  preview: string;
}> = [
  {
    value: 'hardReject',
    label: '直接拦截',
    shortLabel: '拦截',
    description: '明确违规，命中后直接不通过',
    preview: '直接返回不通过',
  },
  {
    value: 'riskSignal',
    label: '交给 AI 判断',
    shortLabel: 'AI 判断',
    description: '结合上下文后再做决定',
    preview: '交给 AI 结合上下文判断',
  },
  {
    value: 'forceReview',
    label: '建议人工复核',
    shortLabel: '需复核',
    description: '不请求 AI，直接返回复核状态',
    preview: '直接返回需要人工复核',
  },
  {
    value: 'contextException',
    label: '作为语境例外',
    shortLabel: '例外',
    description: '减弱同分类的风险信号',
    preview: '减弱同分类的可疑信号',
  },
  {
    value: 'monitorOnly',
    label: '仅记录观察',
    shortLabel: '观察',
    description: '记录命中，但不改变审核结果',
    preview: '只记录这次命中',
  },
];

export const formatRulePresets = [
  { value: 'mobile', label: '中国大陆手机号', pattern: String.raw`1[3-9]\d{9}` },
  {
    value: 'email',
    label: '电子邮箱',
    pattern: String.raw`[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Za-z]{2,}`,
  },
  { value: 'url', label: '网页链接', pattern: String.raw`https?://[^\s]+` },
  { value: 'id-card', label: '中国大陆身份证号', pattern: String.raw`\d{17}[\dXx]` },
  { value: 'custom', label: '自定义格式（高级）', pattern: '' },
];

export interface RuleDraftValidation {
  value?: RuleSetDraftInput;
  errors: string[];
  rowErrors: Record<number, string[]>;
}

export const createEmptyRule = (): WordRuleDraftInput => ({
  term: '',
  type: 'suspicious',
  category: 'contact',
  weight: 0.6,
});

export const createEmptyRegexRule = (): RegexRuleDraftInput => ({
  pattern: formatRulePresets[0].pattern,
  action: 'forceReview',
  category: 'contact',
  weight: 0.8,
  timeoutMs: 100,
  maxInputLength: 65_536,
  engineMode: 'nonBacktracking',
  priority: 0,
});

export const createEmptyCombinationRule = (): CombinationRuleDraftInput => ({
  name: '',
  terms: ['', ''],
  action: 'riskSignal',
  category: 'contact',
  weight: 0.6,
  windowSize: 64,
  priority: 0,
});

export const normalizeRuleType = (
  rule: WordRuleDraftInput,
  type: WordRuleType,
): WordRuleDraftInput => ({
  ...rule,
  type,
  weight: type === 'black' ? 1 : type === 'white' ? 0.1 : 0.6,
});

export const rulesFromRuleSet = (ruleSet?: RuleSet | null): WordRuleDraftInput[] =>
  ruleSet?.rules.length
    ? ruleSet.rules.map(({ id, isEnabled, ...rule }) => {
        void id;
        void isEnabled;
        return rule;
      })
    : [createEmptyRule()];

export const regexRulesFromRuleSet = (ruleSet?: RuleSet | null): RegexRuleDraftInput[] =>
  ruleSet?.regexRules.map(({ id, isEnabled, ...rule }) => {
    void id;
    void isEnabled;
    return rule;
  }) ?? [];

export const combinationRulesFromRuleSet = (
  ruleSet?: RuleSet | null,
): CombinationRuleDraftInput[] =>
  ruleSet?.combinationRules.map(({ id, isEnabled, ...rule }) => {
    void id;
    void isEnabled;
    return rule;
  }) ?? [];

export const legacyTypeForAction = (action: RuleAction): WordRuleType =>
  action === 'hardReject' ? 'black' : action === 'contextException' ? 'white' : 'suspicious';

export const parseKeywordLines = (source: string): { terms: string[]; errors: string[] } => {
  const terms = source
    .split(/\r?\n/)
    .map((term) => term.trim())
    .filter(Boolean);
  const normalized = new Set<string>();
  const uniqueTerms: string[] = [];
  const errors: string[] = [];
  for (const term of terms) {
    const key = term.normalize('NFKC').toUpperCase();
    if (normalized.has(key)) continue;
    normalized.add(key);
    if (term.length > 200) {
      errors.push(`“${term.slice(0, 12)}…”超过 200 个字符`);
    } else {
      uniqueTerms.push(term);
    }
  }
  if (uniqueTerms.length === 0) errors.push('请至少输入一个关键词，每行一个');
  return { terms: uniqueTerms, errors };
};

export const ruleIdentity = (term: string, category: string) =>
  `${term.trim().normalize('NFKC').toUpperCase()}\0${category.trim()}`;

export function createRuleSetDraft(
  name: string,
  rules: WordRuleDraftInput[],
  normalizationProfile: RuleNormalizationProfile = 'default',
  regexRules: RegexRuleDraftInput[] = [],
  combinationRules: CombinationRuleDraftInput[] = [],
): RuleDraftValidation {
  const normalizedName = name.trim();
  const errors: string[] = [];
  const rowErrors: Record<number, string[]> = {};
  const seen = new Map<string, number>();

  if (normalizedName.length < 2 || normalizedName.length > 100) {
    errors.push('规则集名称需要填写 2 到 100 个字符');
  }
  if (rules.length + regexRules.length + combinationRules.length === 0) {
    errors.push('至少需要添加一条规则');
  }

  const normalizedRules = rules.map((rule, index) => {
    const normalized = {
      ...rule,
      term: rule.term.trim(),
      category: rule.category.trim(),
    };
    const issues: string[] = [];
    if (!normalized.term) issues.push('请填写关键词');
    if (normalized.term.length > 200) issues.push('关键词不能超过 200 个字符');
    if (!normalized.category) issues.push('请选择风险分类');
    if (!/^[a-z0-9][a-z0-9._-]{0,63}$/.test(normalized.category)) {
      issues.push('风险分类无效，请重新选择');
    }
    const duplicateKey = ruleIdentity(normalized.term, normalized.category);
    const duplicateIndex = seen.get(duplicateKey);
    if (normalized.term && duplicateIndex !== undefined) {
      issues.push(`与第 ${duplicateIndex + 1} 条同分类关键词重复`);
    } else if (normalized.term) {
      seen.set(duplicateKey, index);
    }
    if (!Number.isFinite(normalized.weight) || normalized.weight < 0 || normalized.weight > 1) {
      issues.push('处理强度无效');
    }
    if (issues.length) rowErrors[index] = issues;
    return normalized;
  });

  regexRules.forEach((rule, index) => {
    if (!rule.pattern.trim()) errors.push(`第 ${index + 1} 条格式规则还没有选择识别格式`);
    if (!rule.category.trim()) errors.push(`第 ${index + 1} 条格式规则还没有选择风险分类`);
  });
  combinationRules.forEach((rule, index) => {
    const terms = rule.terms.map((term) => term.trim()).filter(Boolean);
    if (!rule.name.trim()) errors.push(`第 ${index + 1} 条组合条件还没有填写名称`);
    if (terms.length < 2) errors.push(`第 ${index + 1} 条组合条件至少需要两个关键词`);
    if (!rule.category.trim()) errors.push(`第 ${index + 1} 条组合条件还没有选择风险分类`);
  });

  if (Object.keys(rowErrors).length > 0) errors.push('部分规则还没有填写完整');
  return errors.length > 0
    ? { errors, rowErrors }
    : {
        value: {
          name: normalizedName,
          rules: normalizedRules,
          normalizationProfile,
          regexRules: regexRules.map((rule) => ({ ...rule, pattern: rule.pattern.trim() })),
          combinationRules: combinationRules.map((rule) => ({
            ...rule,
            name: rule.name.trim(),
            terms: rule.terms.map((term) => term.trim()).filter(Boolean),
          })),
        },
        errors: [],
        rowErrors: {},
      };
}
