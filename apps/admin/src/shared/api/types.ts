export type ApplicationStatus = 'active' | 'paused';
export type ApplicationEnvironment = 'live' | 'test';
export type ModerationStatus = 'pass' | 'reject' | 'review';
export type ReviewSource =
  | 'model_ambiguous'
  | 'policy_required'
  | 'provider_refusal'
  | 'ai_failure_fallback';
export type ApiKeyStatus = 'active' | 'revoked' | 'expired';

export interface Application {
  id: string;
  name: string;
  slug: string;
  description: string;
  environment: ApplicationEnvironment | null;
  status: ApplicationStatus;
  policyName: string | null;
  policyVersion: string | null;
  createdAt: string;
  lastActiveAt: string | null;
  totalRequests: number | null;
  reviewRate: number | null;
  rejectRate: number | null;
  activeKeyCount: number;
}

export interface ApiKey {
  id: string;
  applicationId: string;
  name: string;
  prefix: string;
  status: ApiKeyStatus;
  createdAt: string;
  expiresAt: string;
  lastUsedAt: string | null;
  createdBy: string;
  scopes?: string[];
}

export interface OneTimeApiKey {
  key: ApiKey;
  plaintext: string;
}

export interface ModerationRecord {
  id: string;
  applicationId: string;
  applicationName: string;
  contentPreview: string;
  contentHash: string;
  status: ModerationStatus;
  confidence: number | null;
  category: string | null;
  reason: string;
  reviewSource: ReviewSource | null;
  detectLevel: 1 | 2 | null;
  latencyMs: number | null;
  createdAt: string;
  evidence: string[];
  policyVersion: string | null;
}

export interface OverviewStats {
  todayRequests: number | null;
  requestDelta: number | null;
  rejectRate: number | null;
  rejectDelta: number | null;
  reviewRate: number | null;
  reviewDelta: number | null;
  p95LatencyMs: number | null;
  latencyDelta: number | null;
  trend: Array<{ label: string; total: number; reject: number; review: number }>;
  recentRecords: ModerationRecord[];
  decisionRail: Array<{
    label: string;
    value: string;
    tone: 'neutral' | 'teal' | 'red' | 'amber';
    detail: string;
  }>;
}

export interface Paginated<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

export interface ListApplicationsParams {
  keyword?: string;
  status?: ApplicationStatus | 'all';
}

export interface ListRecordsParams {
  applicationId?: string;
  status?: ModerationStatus | 'all';
  keyword?: string;
  page?: number;
  pageSize?: number;
}

export interface CreateApplicationInput {
  name: string;
  slug: string;
  description: string;
  environment: ApplicationEnvironment;
  policyVersion: string;
}

export interface CreateKeyInput {
  applicationId: string;
  name: string;
  expiresAt: string;
}

export interface RevokeKeyInput {
  applicationId: string;
  keyId: string;
  reason: string;
}

export interface ApiErrorShape {
  code: string;
  message: string;
  requestId?: string;
  retryable?: boolean;
  status?: number;
}
