export type ApplicationStatus = 'active' | 'paused';
export type ApplicationEnvironment = 'live' | 'test';
export type ModerationStatus = 'pass' | 'reject' | 'review';
export type ReviewSource =
  | 'model_ambiguous'
  | 'policy_required'
  | 'provider_refusal'
  | 'ai_failure_fallback';
export type ApiKeyStatus = 'active' | 'revoked' | 'expired';
export type AiProtocol = 'openAiChatCompletions' | 'openAiResponses' | 'anthropicMessages';
export type AiAuthScheme = 'bearer' | 'xApiKey' | 'apiKey';
export type AiApiVersionLocation = 'none' | 'header' | 'query';
export type AiDecodingMode = 'sendTemperatureZero' | 'omitTemperature' | 'providerFixed';
export type AiConfigurationStatus = 'draft' | 'published' | 'archived';

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

export interface AiConfigurationDraftInput {
  name: string;
  protocol: AiProtocol;
  baseUrl: string;
  endpointPath: string;
  credentialRef: string;
  authScheme: AiAuthScheme;
  model: string;
  apiVersion?: string | null;
  apiVersionLocation: AiApiVersionLocation;
  systemPrompt: string;
  decodingMode: AiDecodingMode;
  maxInputTokens: number;
  maxOutputTokens: number;
  connectTimeoutMs: number;
  requestTimeoutMs: number;
  maxAttempts: number;
  dataRegion: string;
  retentionClass: string;
}

export interface AiConfiguration extends AiConfigurationDraftInput {
  id: string;
  publicRevisionId: string;
  status: AiConfigurationStatus;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
  publishedAt: string | null;
  lastTestedAt: string | null;
  lastTestSucceeded: boolean | null;
  lastTestFailureCode: string | null;
  adapterContractVersion: string | null;
  canonicalSchemaVersion: string | null;
  canonicalSchemaHash: string | null;
  effectiveSchemaHash: string | null;
  schemaTransformerVersion: string | null;
}

export interface AiConfigurationTestResult {
  succeeded: boolean;
  protocol: string;
  model: string;
  latencyMs: number;
  inputTokens: number | null;
  outputTokens: number | null;
  failureCode: string | null;
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
