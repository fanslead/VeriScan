import type {
  CombinationRule,
  RegexRule,
  RegexRuleEngineMode,
  RuleAction,
  RuleNormalizationProfile,
  RuleSet,
  RuleSetDraftInput,
  RuleSetStatus,
  RuleSetValidationIssue,
  RuleSetValidationResult,
  WordRule,
  WordRuleType,
} from './types';

type ObjectValue = Record<string, unknown>;

const asObject = (value: unknown): ObjectValue =>
  typeof value === 'object' && value !== null ? (value as ObjectValue) : {};
const stringValue = (value: unknown): string => (typeof value === 'string' ? value : '');
const numberValue = (value: unknown): number => {
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
};
const nullableString = (value: unknown): string | null => stringValue(value) || null;
const dateValue = (value: unknown): string => {
  const candidate = stringValue(value);
  return candidate && !Number.isNaN(Date.parse(candidate)) ? candidate : '';
};
const nullableDate = (value: unknown): string | null => dateValue(value) || null;
const statusValue = (value: unknown): RuleSetStatus =>
  value === 'published' || value === 'archived' ? value : 'draft';
const typeValue = (value: unknown): WordRuleType =>
  value === 'black' || value === 'white' ? value : 'suspicious';
const actionValue = (value: unknown, fallback: RuleAction = 'riskSignal'): RuleAction =>
  value === 'hardReject' ||
  value === 'contextException' ||
  value === 'forceReview' ||
  value === 'monitorOnly' ||
  value === 'riskSignal'
    ? value
    : fallback;
const profileValue = (value: unknown): RuleNormalizationProfile =>
  value === 'traditionalSimplified' ? value : 'default';
const regexEngineValue = (value: unknown): RegexRuleEngineMode =>
  value === 'backtracking' ? value : 'nonBacktracking';

const mapRule = (value: unknown): WordRule => {
  const object = asObject(value);
  return {
    id: stringValue(object.id),
    term: stringValue(object.term),
    type: typeValue(object.type),
    category: stringValue(object.category),
    weight: numberValue(object.weight),
    action: object.action ? actionValue(object.action) : null,
    matchMode: 'normalizedContains',
    language: nullableString(object.language),
    scene: nullableString(object.scene),
    evidenceTemplate: nullableString(object.evidenceTemplate),
    priority: numberValue(object.priority),
    source: nullableString(object.source),
    isEnabled: object.isEnabled !== false,
  };
};

const mapRegexRule = (value: unknown): RegexRule => {
  const object = asObject(value);
  return {
    id: stringValue(object.id),
    pattern: stringValue(object.pattern),
    action: actionValue(object.action),
    category: stringValue(object.category),
    weight: numberValue(object.weight),
    timeoutMs: numberValue(object.timeoutMs) || 100,
    maxInputLength: numberValue(object.maxInputLength) || 65_536,
    engineMode: regexEngineValue(object.engineMode),
    language: nullableString(object.language),
    scene: nullableString(object.scene),
    evidenceTemplate: nullableString(object.evidenceTemplate),
    priority: numberValue(object.priority),
    source: nullableString(object.source),
    isEnabled: object.isEnabled !== false,
  };
};

const mapCombinationRule = (value: unknown): CombinationRule => {
  const object = asObject(value);
  return {
    id: stringValue(object.id),
    name: stringValue(object.name),
    terms: Array.isArray(object.terms) ? object.terms.map(stringValue).filter(Boolean) : [],
    action: actionValue(object.action),
    category: stringValue(object.category),
    weight: numberValue(object.weight),
    windowSize: numberValue(object.windowSize) || 64,
    language: nullableString(object.language),
    scene: nullableString(object.scene),
    evidenceTemplate: nullableString(object.evidenceTemplate),
    priority: numberValue(object.priority),
    source: nullableString(object.source),
    isEnabled: object.isEnabled !== false,
  };
};

export function mapRuleSetResponse(value: unknown): RuleSet {
  const object = asObject(value);
  const rules = Array.isArray(object.rules) ? object.rules.map(mapRule) : [];
  const regexRules = Array.isArray(object.regexRules) ? object.regexRules.map(mapRegexRule) : [];
  const combinationRules = Array.isArray(object.combinationRules)
    ? object.combinationRules.map(mapCombinationRule)
    : [];
  return {
    id: stringValue(object.id),
    publicRevisionId: stringValue(object.publicRevisionId),
    name: stringValue(object.name),
    status: statusValue(object.status),
    ruleCount: numberValue(object.ruleCount) || rules.length,
    rulesTruncated: object.rulesTruncated === true,
    createdAt: dateValue(object.createdAt),
    updatedAt: dateValue(object.updatedAt),
    lastValidatedAt: nullableDate(object.lastValidatedAt),
    lastValidatedChecksum: nullableString(object.lastValidatedChecksum),
    publishedAt: nullableDate(object.publishedAt),
    publishedChecksum: nullableString(object.publishedChecksum),
    applicationCount: numberValue(object.applicationCount),
    rules,
    normalizationProfile: profileValue(object.normalizationProfile),
    regexRules,
    combinationRules,
  };
}

export function mapRuleSetListResponse(value: unknown): RuleSet[] {
  const object = asObject(value);
  return (Array.isArray(object.items) ? object.items : []).map(mapRuleSetResponse);
}

export function mapRuleSetValidationResponse(value: unknown): RuleSetValidationResult {
  const object = asObject(value);
  const issues: RuleSetValidationIssue[] = (Array.isArray(object.issues) ? object.issues : []).map(
    (issue) => {
      const item = asObject(issue);
      return {
        code: stringValue(item.code),
        message: stringValue(item.message),
        ruleIndex: item.ruleIndex === null ? null : numberValue(item.ruleIndex),
      };
    },
  );
  return {
    valid: object.valid === true,
    checksum: stringValue(object.checksum),
    ruleCount: numberValue(object.ruleCount),
    issues,
  };
}

export function mapRuleSetDraftInput(input: RuleSetDraftInput): RuleSetDraftInput {
  return {
    name: input.name.trim(),
    normalizationProfile: input.normalizationProfile,
    rules: input.rules.map((rule) => ({
      ...rule,
      term: rule.term.trim(),
      category: rule.category.trim().toLowerCase(),
      language: rule.language?.trim() || null,
      scene: rule.scene?.trim() || null,
      evidenceTemplate: rule.evidenceTemplate?.trim() || null,
      source: rule.source?.trim() || null,
    })),
    regexRules: input.regexRules.map((rule) => ({
      ...rule,
      pattern: rule.pattern.trim(),
      category: rule.category.trim().toLowerCase(),
      language: rule.language?.trim() || null,
      scene: rule.scene?.trim() || null,
      evidenceTemplate: rule.evidenceTemplate?.trim() || null,
      source: rule.source?.trim() || null,
    })),
    combinationRules: input.combinationRules.map((rule) => ({
      ...rule,
      name: rule.name.trim(),
      terms: rule.terms.map((term) => term.trim()).filter(Boolean),
      category: rule.category.trim().toLowerCase(),
      language: rule.language?.trim() || null,
      scene: rule.scene?.trim() || null,
      evidenceTemplate: rule.evidenceTemplate?.trim() || null,
      source: rule.source?.trim() || null,
    })),
  };
}
