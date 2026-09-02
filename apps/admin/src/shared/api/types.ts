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
export type RuleSetStatus = 'draft' | 'published' | 'archived';
export type WordRuleType = 'black' | 'suspicious' | 'white';
export type RuleAction =
  | 'hardReject'
  | 'riskSignal'
  | 'contextException'
  | 'forceReview'
  | 'monitorOnly';
export type RuleMatchMode = 'normalizedContains';
export type RuleNormalizationProfile = 'default' | 'traditionalSimplified';
export type RegexRuleEngineMode = 'nonBacktracking' | 'backtracking';

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

export type WebhookTestStatus = 'pending' | 'delivering' | 'succeeded' | 'failed';

export interface ApplicationWebhook {
  configured: boolean;
  id: string | null;
  applicationId: string;
  endpointUrl: string | null;
  enabled: boolean;
  revision: number | null;
  currentRevisionTested: boolean;
  lastTestId: string | null;
  lastTestStatus: WebhookTestStatus | null;
  lastTestHttpStatusCode: number | null;
  lastTestLatencyMilliseconds: number | null;
  lastTestedAt: string | null;
  updatedAt: string | null;
}

export interface ApplicationWebhookSaved {
  webhook: ApplicationWebhook;
  signingSecret: string | null;
}

export interface ApplicationWebhookSecret {
  signingSecret: string;
  rotatedAt: string;
}

export interface ApplicationWebhookTestAccepted {
  testId: string;
  statusUrl: string;
  submittedAt: string;
}

export interface ApplicationWebhookTest {
  testId: string;
  applicationId: string;
  configurationRevision: number;
  status: WebhookTestStatus;
  httpStatusCode: number | null;
  latencyMilliseconds: number | null;
  failureCode: string | null;
  submittedAt: string;
  completedAt: string | null;
}

export interface ApplicationUsage {
  applicationId: string;
  apiKeyId: string | null;
  dataFrom: string;
  dataThrough: string;
  requestCount: number;
  itemCount: number;
  passCount: number;
  rejectCount: number;
  reviewCount: number;
  aiCallCount: number;
  aiInputTokens: number | null;
  aiOutputTokens: number | null;
  aiFailureCount: number;
}

export interface AuditEvent {
  id: string;
  tenantId: string | null;
  applicationId: string | null;
  apiKeyId: string | null;
  actorType: string;
  actorId: string | null;
  action: string;
  resourceType: string;
  resourceId: string;
  beforeJson: string | null;
  afterJson: string | null;
  correlationId: string | null;
  occurredAt: string;
}

export interface AuditEventList {
  items: AuditEvent[];
  total: number;
  dataFrom: string;
  dataThrough: string;
}

export interface ListAuditEventsParams {
  applicationId?: string;
  action?: string;
  from?: string;
  through?: string;
  limit?: number;
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
  apiKey: string;
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

export interface AiConfiguration extends Omit<AiConfigurationDraftInput, 'apiKey'> {
  id: string;
  publicRevisionId: string;
  credentialRef: string;
  hasCredential: boolean;
  credentialSource: 'managed' | 'server';
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

export interface WordRuleDraftInput {
  term: string;
  type: WordRuleType;
  category: string;
  weight: number;
  action?: RuleAction | null;
  matchMode?: RuleMatchMode;
  language?: string | null;
  scene?: string | null;
  evidenceTemplate?: string | null;
  priority?: number;
  source?: string | null;
}

export interface WordRule extends WordRuleDraftInput {
  id: string;
  isEnabled: boolean;
}

export interface RegexRuleDraftInput {
  pattern: string;
  action: RuleAction;
  category: string;
  weight: number;
  timeoutMs: number;
  maxInputLength: number;
  engineMode: RegexRuleEngineMode;
  language?: string | null;
  scene?: string | null;
  evidenceTemplate?: string | null;
  priority?: number;
  source?: string | null;
}

export interface RegexRule extends RegexRuleDraftInput {
  id: string;
  isEnabled: boolean;
}

export interface CombinationRuleDraftInput {
  name: string;
  terms: string[];
  action: RuleAction;
  category: string;
  weight: number;
  windowSize: number;
  language?: string | null;
  scene?: string | null;
  evidenceTemplate?: string | null;
  priority?: number;
  source?: string | null;
}

export interface CombinationRule extends CombinationRuleDraftInput {
  id: string;
  isEnabled: boolean;
}

export interface RuleSetDraftInput {
  name: string;
  rules: WordRuleDraftInput[];
  normalizationProfile: RuleNormalizationProfile;
  regexRules: RegexRuleDraftInput[];
  combinationRules: CombinationRuleDraftInput[];
}

export interface RuleSet {
  id: string;
  publicRevisionId: string;
  name: string;
  status: RuleSetStatus;
  ruleCount: number;
  rulesTruncated: boolean;
  createdAt: string;
  updatedAt: string;
  lastValidatedAt: string | null;
  lastValidatedChecksum: string | null;
  publishedAt: string | null;
  publishedChecksum: string | null;
  applicationCount: number;
  rules: WordRule[];
  normalizationProfile: RuleNormalizationProfile;
  regexRules: RegexRule[];
  combinationRules: CombinationRule[];
}

export interface RuleSetValidationIssue {
  code: string;
  message: string;
  ruleIndex: number | null;
}

export interface RuleSetValidationResult {
  valid: boolean;
  checksum: string;
  ruleCount: number;
  issues: RuleSetValidationIssue[];
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
