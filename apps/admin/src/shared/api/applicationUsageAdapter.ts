import type { ApplicationUsage } from './types';

type ObjectValue = Record<string, unknown>;

const asObject = (value: unknown): ObjectValue =>
  typeof value === 'object' && value !== null ? (value as ObjectValue) : {};

const stringValue = (value: unknown): string => (typeof value === 'string' ? value : '');

const nonNegativeNumber = (value: unknown): number => {
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : 0;
};

const nullableNonNegativeNumber = (value: unknown): number | null => {
  if (value === null || value === undefined) return null;
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) && parsed >= 0 ? parsed : null;
};

const dateValue = (value: unknown): string => {
  const candidate = stringValue(value);
  return candidate && !Number.isNaN(Date.parse(candidate)) ? candidate : '';
};

export function mapApplicationUsageResponse(value: unknown): ApplicationUsage {
  const object = asObject(value);
  return {
    applicationId: stringValue(object.applicationId),
    apiKeyId: object.apiKeyId === null ? null : stringValue(object.apiKeyId) || null,
    dataFrom: dateValue(object.dataFrom),
    dataThrough: dateValue(object.dataThrough),
    requestCount: nonNegativeNumber(object.requestCount),
    itemCount: nonNegativeNumber(object.itemCount),
    passCount: nonNegativeNumber(object.passCount),
    rejectCount: nonNegativeNumber(object.rejectCount),
    reviewCount: nonNegativeNumber(object.reviewCount),
    aiCallCount: nonNegativeNumber(object.aiCallCount),
    aiInputTokens: nullableNonNegativeNumber(object.aiInputTokens),
    aiOutputTokens: nullableNonNegativeNumber(object.aiOutputTokens),
    aiFailureCount: nonNegativeNumber(object.aiFailureCount),
  };
}
