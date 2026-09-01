import type {
  AiAuthScheme,
  AiApiVersionLocation,
  AiConfiguration,
  AiConfigurationDraftInput,
  AiConfigurationStatus,
  AiConfigurationTestResult,
  AiDecodingMode,
  AiProtocol,
} from './types';

export interface AiConfigurationResponseDto {
  id?: string;
  publicRevisionId?: string;
  name?: string;
  protocol?: string;
  baseUrl?: string;
  endpointPath?: string;
  credentialRef?: string;
  hasCredential?: boolean;
  credentialSource?: string;
  authScheme?: string;
  model?: string;
  apiVersion?: string | null;
  apiVersionLocation?: string;
  systemPrompt?: string;
  decodingMode?: string;
  maxInputTokens?: number;
  maxOutputTokens?: number;
  connectTimeoutMs?: number;
  requestTimeoutMs?: number;
  maxAttempts?: number;
  dataRegion?: string;
  retentionClass?: string;
  status?: string;
  isActive?: boolean;
  createdAt?: string;
  updatedAt?: string;
  publishedAt?: string | null;
  lastTestedAt?: string | null;
  lastTestSucceeded?: boolean | null;
  lastTestFailureCode?: string | null;
  adapterContractVersion?: string | null;
  canonicalSchemaVersion?: string | null;
  canonicalSchemaHash?: string | null;
  effectiveSchemaHash?: string | null;
  schemaTransformerVersion?: string | null;
}

export interface AiConfigurationListResponseDto {
  items?: AiConfigurationResponseDto[];
}

export interface AiConfigurationTestResponseDto {
  succeeded?: boolean;
  protocol?: string;
  model?: string;
  latencyMs?: number;
  inputTokens?: number | null;
  outputTokens?: number | null;
  failureCode?: string | null;
}

type ObjectValue = Record<string, unknown>;

const asObject = (value: unknown): ObjectValue =>
  typeof value === 'object' && value !== null ? (value as ObjectValue) : {};

const stringValue = (value: unknown): string => (typeof value === 'string' ? value : '');

const nullableStringValue = (value: unknown): string | null => {
  if (value === null || value === undefined || value === '') return null;
  return stringValue(value) || null;
};

const numberValue = (value: unknown, fallback: number): number => {
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : fallback;
};

const nullableNumberValue = (value: unknown): number | null => {
  if (value === null || value === undefined || value === '') return null;
  const parsed = typeof value === 'number' ? value : Number(value);
  return Number.isFinite(parsed) ? parsed : null;
};

const dateValue = (value: unknown): string => {
  const candidate = stringValue(value);
  return candidate && !Number.isNaN(Date.parse(candidate)) ? candidate : '';
};

const protocolValue = (value: unknown): AiProtocol => {
  if (value === 'openAiResponses') return 'openAiResponses';
  if (value === 'anthropicMessages') return 'anthropicMessages';
  return 'openAiChatCompletions';
};

const authSchemeValue = (value: unknown): AiAuthScheme => {
  if (value === 'xApiKey') return 'xApiKey';
  if (value === 'apiKey') return 'apiKey';
  return 'bearer';
};

const decodingModeValue = (value: unknown): AiDecodingMode => {
  if (value === 'sendTemperatureZero') return 'sendTemperatureZero';
  if (value === 'providerFixed') return 'providerFixed';
  return 'omitTemperature';
};

const apiVersionLocationValue = (value: unknown): AiApiVersionLocation => {
  if (value === 'header') return 'header';
  if (value === 'query') return 'query';
  return 'none';
};

const statusValue = (value: unknown): AiConfigurationStatus => {
  if (value === 'published') return 'published';
  if (value === 'archived') return 'archived';
  return 'draft';
};

export function mapAiConfigurationResponse(value: unknown): AiConfiguration {
  const object = asObject(value);
  const status = statusValue(object.status);
  return {
    id: stringValue(object.id),
    publicRevisionId: stringValue(object.publicRevisionId),
    name: stringValue(object.name),
    protocol: protocolValue(object.protocol),
    baseUrl: stringValue(object.baseUrl),
    endpointPath: stringValue(object.endpointPath),
    credentialRef: stringValue(object.credentialRef),
    hasCredential: object.hasCredential === true,
    credentialSource: object.credentialSource === 'managed' ? 'managed' : 'server',
    authScheme: authSchemeValue(object.authScheme),
    model: stringValue(object.model),
    apiVersion: nullableStringValue(object.apiVersion),
    apiVersionLocation: apiVersionLocationValue(object.apiVersionLocation),
    systemPrompt: stringValue(object.systemPrompt),
    decodingMode: decodingModeValue(object.decodingMode),
    maxInputTokens: numberValue(object.maxInputTokens, 4096),
    maxOutputTokens: numberValue(object.maxOutputTokens, 512),
    connectTimeoutMs: numberValue(object.connectTimeoutMs, 2000),
    requestTimeoutMs: numberValue(object.requestTimeoutMs, 15000),
    maxAttempts: numberValue(object.maxAttempts, 2),
    dataRegion: stringValue(object.dataRegion),
    retentionClass: stringValue(object.retentionClass),
    status,
    isActive: status === 'published' && object.isActive === true,
    createdAt: dateValue(object.createdAt),
    updatedAt: dateValue(object.updatedAt),
    publishedAt: object.publishedAt === null ? null : dateValue(object.publishedAt) || null,
    lastTestedAt: object.lastTestedAt === null ? null : dateValue(object.lastTestedAt) || null,
    lastTestSucceeded:
      typeof object.lastTestSucceeded === 'boolean' ? object.lastTestSucceeded : null,
    lastTestFailureCode: nullableStringValue(object.lastTestFailureCode),
    adapterContractVersion: nullableStringValue(object.adapterContractVersion),
    canonicalSchemaVersion: nullableStringValue(object.canonicalSchemaVersion),
    canonicalSchemaHash: nullableStringValue(object.canonicalSchemaHash),
    effectiveSchemaHash: nullableStringValue(object.effectiveSchemaHash),
    schemaTransformerVersion: nullableStringValue(object.schemaTransformerVersion),
  };
}

export function mapAiConfigurationListResponse(value: unknown): AiConfiguration[] {
  const object = asObject(value);
  const items = Array.isArray(value) ? value : Array.isArray(object.items) ? object.items : [];
  return items.map(mapAiConfigurationResponse);
}

export function mapAiConfigurationTestResponse(value: unknown): AiConfigurationTestResult {
  const object = asObject(value);
  return {
    succeeded: object.succeeded === true,
    protocol: stringValue(object.protocol),
    model: stringValue(object.model),
    latencyMs: numberValue(object.latencyMs, 0),
    inputTokens: nullableNumberValue(object.inputTokens),
    outputTokens: nullableNumberValue(object.outputTokens),
    failureCode: nullableStringValue(object.failureCode),
  };
}

export function mapAiConfigurationDraftInput(
  value: AiConfigurationDraftInput,
): AiConfigurationDraftInput {
  return {
    name: value.name.trim(),
    protocol: value.protocol,
    baseUrl: value.baseUrl.trim(),
    endpointPath: value.endpointPath.trim(),
    apiKey: value.apiKey.trim(),
    authScheme: value.authScheme,
    model: value.model.trim(),
    apiVersion: value.apiVersion?.trim() || null,
    apiVersionLocation: value.apiVersionLocation,
    systemPrompt: value.systemPrompt.trim(),
    decodingMode: value.decodingMode,
    maxInputTokens: value.maxInputTokens,
    maxOutputTokens: value.maxOutputTokens,
    connectTimeoutMs: value.connectTimeoutMs,
    requestTimeoutMs: value.requestTimeoutMs,
    maxAttempts: value.maxAttempts,
    dataRegion: value.dataRegion.trim(),
    retentionClass: value.retentionClass.trim(),
  };
}
