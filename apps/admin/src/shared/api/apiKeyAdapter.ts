import type { ApiKey, ApiKeyStatus, OneTimeApiKey } from './types';

export interface ApiKeySummaryResponseDto {
  keyId: string;
  keyPrefix: string;
  lastFour: string;
  scopes: string[];
  environment: 'test' | 'live' | null;
  status: 'active' | 'revoked' | 'expired';
  notBefore: string;
  expiresAt: string;
  createdAt: string;
  revokedAt: string | null;
  lastUsedAt: string | null;
  displayName?: string | null;
}

export interface ApiKeyCreatedResponseDto {
  keyId: string;
  keyPrefix: string;
  apiKey: string;
  scopes: string[];
  expiresAt: string;
  displayName?: string | null;
}

type ObjectValue = Record<string, unknown>;

const asObject = (value: unknown): ObjectValue =>
  typeof value === 'object' && value !== null ? (value as ObjectValue) : {};

const stringValue = (value: unknown): string => (typeof value === 'string' ? value : '');

const nullableStringValue = (value: unknown): string | null => {
  if (value === null || value === undefined || value === '') return null;
  return stringValue(value) || null;
};

const stringArray = (value: unknown): string[] =>
  Array.isArray(value)
    ? value.filter((item): item is string => typeof item === 'string')
    : typeof value === 'string'
      ? value
          .split(',')
          .map((item) => item.trim())
          .filter(Boolean)
      : [];

const dateValue = (value: unknown): string => {
  const candidate = stringValue(value);
  return candidate && !Number.isNaN(Date.parse(candidate)) ? candidate : '';
};

const mapStatus = (value: unknown, expiresAt: string): ApiKeyStatus => {
  if (value === 'revoked') return 'revoked';
  if (value === 'expired' || (expiresAt && Date.parse(expiresAt) <= Date.now())) return 'expired';
  return 'active';
};

interface ApiKeyContext {
  applicationId: string;
  name?: string;
}

const mapViewKey = (object: ObjectValue, context: ApiKeyContext): ApiKey => {
  const expiresAt = dateValue(object.expiresAt);
  return {
    id: stringValue(object.id),
    applicationId: context.applicationId,
    name: stringValue(object.name) || context.name || stringValue(object.prefix),
    prefix: stringValue(object.prefix),
    status: mapStatus(object.status, expiresAt),
    createdAt: dateValue(object.createdAt),
    expiresAt,
    lastUsedAt: nullableStringValue(object.lastUsedAt),
    createdBy: stringValue(object.createdBy),
    scopes: stringArray(object.scopes),
  };
};

const mapTransportKey = (object: ObjectValue, context: ApiKeyContext): ApiKey => {
  const expiresAt = dateValue(object.expiresAt);
  return {
    id: stringValue(object.keyId),
    applicationId: context.applicationId,
    name: stringValue(object.displayName) || context.name || stringValue(object.keyPrefix),
    prefix: stringValue(object.keyPrefix),
    status: mapStatus(object.status, expiresAt),
    createdAt: dateValue(object.createdAt),
    expiresAt,
    lastUsedAt: nullableStringValue(object.lastUsedAt),
    createdBy: '',
    scopes: stringArray(object.scopes),
  };
};

export function mapApiKeySummaryResponse(value: unknown, context: ApiKeyContext): ApiKey {
  const object = asObject(value);
  if ('applicationId' in object || 'name' in object || 'prefix' in object) {
    return mapViewKey(object, context);
  }
  return mapTransportKey(object, context);
}

export function mapApiKeyListResponse(value: unknown, applicationId: string): ApiKey[] {
  const object = asObject(value);
  const items: unknown[] = Array.isArray(value)
    ? value
    : Array.isArray(object.items)
      ? object.items
      : [];
  return items.map((item) => mapApiKeySummaryResponse(item, { applicationId }));
}

export function mapCreatedApiKeyResponse(value: unknown, context: ApiKeyContext): OneTimeApiKey {
  const object = asObject(value);
  const nested = asObject(object.key);
  if (Object.keys(nested).length > 0 && ('plaintext' in object || 'apiKey' in object)) {
    return {
      key: mapApiKeySummaryResponse(nested, context),
      plaintext: stringValue(object.plaintext) || stringValue(object.apiKey),
    };
  }
  const key = mapApiKeySummaryResponse(
    {
      ...object,
      displayName: object.displayName ?? context.name,
    },
    context,
  );
  return {
    key,
    plaintext: stringValue(object.apiKey),
  };
}
