import type {
  ModerationRecord,
  ModerationStatus,
  OverviewStats,
  Paginated,
  ReviewSource,
} from './types';

export interface ModerationRecordResponseDto {
  id?: string;
  recordId?: string;
  itemId?: string;
  requestId?: string;
  applicationId?: string;
  applicationName?: string;
  contentPreview?: string;
  content?: string;
  contentHash?: string;
  status?: string;
  finalStatus?: string;
  processingStatus?: string;
  decision?: string | null;
  reviewRequired?: boolean;
  reviewSource?: string | null;
  riskScore?: number | null;
  confidence?: number | null;
  reason?: string;
  reasonCodes?: string[];
  categories?: Array<{ code?: string; riskScore?: number | null }>;
  category?: string | null;
  categoryCode?: string | null;
  detectLevel?: number | null;
  latencyMs?: number | null;
  createdAt?: string;
  submittedAt?: string;
  machineCompletedAt?: string | null;
  finalizedAt?: string | null;
  evidence?: string[];
  policyVersion?: string;
}

export interface OverviewTrendDto {
  label?: string;
  total?: number;
  reject?: number;
  review?: number;
}

export interface OverviewDecisionRailDto {
  label?: string;
  value?: string;
  tone?: string;
  detail?: string;
}

export interface OverviewResponseDto {
  todayRequests?: number;
  requestDelta?: number;
  rejectRate?: number;
  rejectDelta?: number;
  reviewRate?: number;
  reviewDelta?: number;
  p95LatencyMs?: number;
  latencyDelta?: number;
  trend?: OverviewTrendDto[];
  recentRecords?: ModerationRecordResponseDto[];
  decisionRail?: OverviewDecisionRailDto[];
}

type ObjectValue = Record<string, unknown>;

const asObject = (value: unknown): ObjectValue =>
  typeof value === 'object' && value !== null ? (value as ObjectValue) : {};

const stringValue = (value: unknown): string => (typeof value === 'string' ? value : '');

const numberValue = (value: unknown): number => {
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : 0;
};

const nullableNumberValue = (value: unknown): number | null => {
  if (value === null || value === undefined || value === '') return null;
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : null;
};

const nullableDateValue = (value: unknown): string | null => {
  if (value === null || value === undefined || value === '') return null;
  const candidate = stringValue(value);
  return candidate && !Number.isNaN(Date.parse(candidate)) ? candidate : null;
};

const stringArray = (value: unknown): string[] =>
  Array.isArray(value) ? value.filter((item): item is string => typeof item === 'string') : [];

const moderationStatus = (object: ObjectValue): ModerationStatus => {
  const status = stringValue(object.status || object.finalStatus || object.decision).toLowerCase();
  if (object.reviewRequired === true || status === 'review' || status === 'manual_review')
    return 'review';
  if (status === 'reject' || status === 'rejected' || status === 'block') return 'reject';
  return 'pass';
};

const normalizeScore = (value: unknown): number | null => {
  const score = nullableNumberValue(value);
  if (score === null) return null;
  return score > 1 ? score / 100 : score;
};

const reviewSource = (value: unknown): ReviewSource | null => {
  if (
    value === 'model_ambiguous' ||
    value === 'policy_required' ||
    value === 'provider_refusal' ||
    value === 'ai_failure_fallback'
  ) {
    return value;
  }
  return null;
};

const mapViewRecord = (object: ObjectValue): ModerationRecord => ({
  id: stringValue(object.id),
  applicationId: stringValue(object.applicationId),
  applicationName: stringValue(object.applicationName),
  contentPreview: stringValue(object.contentPreview),
  contentHash: stringValue(object.contentHash),
  status: moderationStatus(object),
  confidence: normalizeScore(object.confidence),
  category: stringValue(object.category) || null,
  reason: stringValue(object.reason),
  reviewSource: reviewSource(object.reviewSource),
  detectLevel: object.detectLevel === 2 ? 2 : object.detectLevel === 1 ? 1 : null,
  latencyMs: nullableNumberValue(object.latencyMs),
  createdAt: nullableDateValue(object.createdAt) ?? '',
  evidence: stringArray(object.evidence),
  policyVersion: stringValue(object.policyVersion) || null,
});

export function mapModerationRecordResponse(value: unknown): ModerationRecord {
  const object = asObject(value);
  if (
    'confidence' in object ||
    'latencyMs' in object ||
    'policyVersion' in object ||
    'evidence' in object
  ) {
    return mapViewRecord(object);
  }
  const categories = Array.isArray(object.categories) ? object.categories : [];
  const firstCategory = asObject(categories[0]);
  const reasonCodes = stringArray(object.reasonCodes);
  const evidence = stringArray(object.evidence);
  const category =
    stringValue(object.category) ||
    stringValue(object.categoryCode) ||
    stringValue(firstCategory.code) ||
    null;
  const createdAt =
    nullableDateValue(object.createdAt) ??
    nullableDateValue(object.submittedAt) ??
    nullableDateValue(object.machineCompletedAt) ??
    nullableDateValue(object.finalizedAt) ??
    '';
  return {
    id:
      stringValue(object.id) ||
      stringValue(object.recordId) ||
      stringValue(object.itemId) ||
      stringValue(object.requestId),
    applicationId: stringValue(object.applicationId),
    applicationName: stringValue(object.applicationName),
    contentPreview: stringValue(object.contentPreview) || stringValue(object.content),
    contentHash: stringValue(object.contentHash),
    status: moderationStatus(object),
    confidence: normalizeScore(object.confidence ?? object.riskScore),
    category,
    reason: stringValue(object.reason) || reasonCodes.join('、'),
    reviewSource: reviewSource(object.reviewSource),
    detectLevel: object.detectLevel === 2 ? 2 : object.detectLevel === 1 ? 1 : null,
    latencyMs: nullableNumberValue(object.latencyMs),
    createdAt,
    evidence: evidence.length > 0 ? evidence : reasonCodes,
    policyVersion: stringValue(object.policyVersion) || null,
  };
}

export function mapModerationRecordListResponse(value: unknown): Paginated<ModerationRecord> {
  if (Array.isArray(value)) {
    const items = value.map(mapModerationRecordResponse);
    return { items, total: items.length, page: 1, pageSize: items.length };
  }
  const object = asObject(value);
  const items = Array.isArray(object.items) ? object.items.map(mapModerationRecordResponse) : [];
  return {
    items,
    total: numberValue(object.totalCount ?? object.total),
    page: numberValue(object.page) || 1,
    pageSize: numberValue(object.pageSize) || items.length,
  };
}

export function mapOverviewResponse(value: unknown): OverviewStats {
  const object = asObject(value);
  const trend = Array.isArray(object.trend)
    ? object.trend.map((item) => {
        const row = asObject(item);
        return {
          label: stringValue(row.label),
          total: numberValue(row.total),
          reject: numberValue(row.reject),
          review: numberValue(row.review),
        };
      })
    : [];
  const recentRecords = Array.isArray(object.recentRecords)
    ? object.recentRecords.map(mapModerationRecordResponse)
    : [];
  const decisionRail = Array.isArray(object.decisionRail)
    ? object.decisionRail.map((item) => {
        const row = asObject(item);
        const tone = row.tone;
        return {
          label: stringValue(row.label),
          value: stringValue(row.value),
          tone: (tone === 'teal' || tone === 'red' || tone === 'amber' ? tone : 'neutral') as
            | 'neutral'
            | 'teal'
            | 'red'
            | 'amber',
          detail: stringValue(row.detail),
        };
      })
    : [];
  return {
    todayRequests: nullableNumberValue(object.todayRequests),
    requestDelta: nullableNumberValue(object.requestDelta),
    rejectRate: nullableNumberValue(object.rejectRate),
    rejectDelta: nullableNumberValue(object.rejectDelta),
    reviewRate: nullableNumberValue(object.reviewRate),
    reviewDelta: nullableNumberValue(object.reviewDelta),
    p95LatencyMs: nullableNumberValue(object.p95LatencyMs),
    latencyDelta: nullableNumberValue(object.latencyDelta),
    trend,
    recentRecords,
    decisionRail,
  };
}
