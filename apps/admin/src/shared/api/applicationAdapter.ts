import type { Application, ApplicationEnvironment, ApplicationStatus, Paginated } from './types';

export interface ApplicationResponseDto {
  id: string;
  publicId: string;
  name: string;
  environment: 'test' | 'live' | null;
  status: 'active' | 'suspended' | 'archived';
  activeKeyCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface ApplicationListResponseDto {
  items: ApplicationResponseDto[];
  totalCount: number;
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

const dateValue = (value: unknown): string => {
  const candidate = stringValue(value);
  return candidate && !Number.isNaN(Date.parse(candidate)) ? candidate : '';
};

const environmentValue = (value: unknown): ApplicationEnvironment | null => {
  if (value === 'live' || value === 'test') return value;
  return null;
};

const mapStatus = (value: unknown): ApplicationStatus => (value === 'active' ? 'active' : 'paused');

const toTransport = (value: unknown): ApplicationResponseDto => {
  const object = asObject(value);
  return {
    id: stringValue(object.id),
    publicId: stringValue(object.publicId),
    name: stringValue(object.name),
    environment:
      object.environment === 'test' ? 'test' : object.environment === 'live' ? 'live' : null,
    status:
      object.status === 'active'
        ? 'active'
        : object.status === 'archived'
          ? 'archived'
          : 'suspended',
    activeKeyCount: numberValue(object.activeKeyCount),
    createdAt: dateValue(object.createdAt),
    updatedAt: dateValue(object.updatedAt),
  };
};

const mapViewApplication = (value: ObjectValue): Application => ({
  id: stringValue(value.id),
  name: stringValue(value.name),
  slug: stringValue(value.slug),
  description: stringValue(value.description),
  environment: environmentValue(value.environment),
  status: mapStatus(value.status),
  policyName: stringValue(value.policyName) || null,
  policyVersion: stringValue(value.policyVersion) || null,
  createdAt: dateValue(value.createdAt),
  lastActiveAt: value.lastActiveAt === null ? null : dateValue(value.lastActiveAt) || null,
  totalRequests: nullableNumberValue(value.totalRequests),
  reviewRate: nullableNumberValue(value.reviewRate),
  rejectRate: nullableNumberValue(value.rejectRate),
  activeKeyCount: numberValue(value.activeKeyCount),
});

export function mapApplicationResponse(value: unknown): Application {
  const object = asObject(value);
  if ('slug' in object || 'policyName' in object || 'totalRequests' in object) {
    return mapViewApplication(object);
  }
  const dto = toTransport(value);
  return {
    id: dto.id,
    name: dto.name,
    slug: dto.publicId,
    description: '',
    environment: environmentValue(dto.environment),
    status: mapStatus(dto.status),
    policyName: null,
    policyVersion: null,
    createdAt: dto.createdAt,
    lastActiveAt: null,
    totalRequests: null,
    reviewRate: null,
    rejectRate: null,
    activeKeyCount: dto.activeKeyCount,
  };
}

export function mapApplicationListResponse(value: unknown): Paginated<Application> {
  if (Array.isArray(value)) {
    const items = value.map(mapApplicationResponse);
    return { items, total: items.length, page: 1, pageSize: items.length };
  }
  const object = asObject(value);
  const items = Array.isArray(object.items) ? object.items.map(mapApplicationResponse) : [];
  return {
    items,
    total: numberValue(object.totalCount ?? object.total),
    page: numberValue(object.page) || 1,
    pageSize: numberValue(object.pageSize) || items.length,
  };
}
