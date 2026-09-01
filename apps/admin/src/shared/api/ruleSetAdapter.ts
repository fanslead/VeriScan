import type {
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

const mapRule = (value: unknown): WordRule => {
  const object = asObject(value);
  return {
    id: stringValue(object.id),
    term: stringValue(object.term),
    type: typeValue(object.type),
    category: stringValue(object.category),
    weight: numberValue(object.weight),
    isEnabled: object.isEnabled !== false,
  };
};

export function mapRuleSetResponse(value: unknown): RuleSet {
  const object = asObject(value);
  const rules = Array.isArray(object.rules) ? object.rules.map(mapRule) : [];
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
    rules: input.rules.map((rule) => ({
      term: rule.term.trim(),
      type: rule.type,
      category: rule.category.trim().toLowerCase(),
      weight: rule.weight,
    })),
  };
}
