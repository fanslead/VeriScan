import type { AxiosRequestConfig } from 'axios';
import type { ApiClient } from './httpClient';
import { apiKeys, applications, moderationRecords, overviewStats } from './mockData';
import type {
  ApiErrorShape,
  ApiKey,
  Application,
  CreateApplicationInput,
  CreateKeyInput,
  ListApplicationsParams,
  ListRecordsParams,
  ModerationRecord,
  Paginated,
  RevokeKeyInput,
} from './types';

export class MockApiError extends Error {
  readonly shape: ApiErrorShape;

  constructor(shape: ApiErrorShape) {
    super(shape.message);
    this.name = 'MockApiError';
    this.shape = shape;
  }
}

const wait = (milliseconds = 240) =>
  new Promise<void>((resolve) => window.setTimeout(resolve, milliseconds));

const result = <T>(value: T): T => structuredClone(value);

const notFound = (message: string) => {
  throw new MockApiError({ code: 'not_found', message, retryable: false });
};

const makePlaintextKey = (environment: 'live' | 'test') => {
  const publicIdBytes = new Uint8Array(16);
  const secretBytes = new Uint8Array(32);
  crypto.getRandomValues(publicIdBytes);
  crypto.getRandomValues(secretBytes);
  const publicId = Array.from(publicIdBytes, (byte) => byte.toString(16).padStart(2, '0')).join('');
  const secret = btoa(String.fromCharCode(...secretBytes))
    .replace(/\+/g, '-')
    .replace(/\//g, '_')
    .replace(/=+$/g, '');
  return `vsk_${environment}_${publicId}.${secret}`;
};

const makePrefix = (plaintext: string) => plaintext.split('.')[0];

const findApplication = (id: string) => applications.find((item) => item.id === id);

const findKey = (id: string) => apiKeys.find((item) => item.id === id);

const parseQuery = (path: string) => {
  const url = new URL(path, window.location.origin);
  return { pathname: url.pathname, search: url.searchParams };
};

export class MockApiClient implements ApiClient {
  async get<T>(path: string): Promise<T> {
    await wait();
    const { pathname, search } = parseQuery(path);

    if (pathname === '/overview') {
      return result(overviewStats) as T;
    }

    if (pathname === '/applications') {
      const params: ListApplicationsParams = {
        keyword: search.get('keyword') ?? undefined,
        status: (search.get('status') as ListApplicationsParams['status']) ?? 'all',
      };
      const filtered = applications.filter((item) => {
        const matchesKeyword =
          !params.keyword ||
          `${item.name} ${item.slug}`.toLowerCase().includes(params.keyword.toLowerCase());
        const matchesStatus =
          !params.status || params.status === 'all' || item.status === params.status;
        return matchesKeyword && matchesStatus;
      });
      return result({
        items: filtered,
        total: filtered.length,
        page: 1,
        pageSize: filtered.length,
      }) as T;
    }

    const applicationMatch = pathname.match(/^\/applications\/([^/]+)$/);
    if (applicationMatch) {
      const application = findApplication(applicationMatch[1]);
      return application ? (result(application) as T) : notFound('应用不存在');
    }

    const keyMatch = pathname.match(/^\/applications\/([^/]+)\/(?:api-keys|keys)$/);
    if (keyMatch) {
      return result(apiKeys.filter((item) => item.applicationId === keyMatch[1])) as T;
    }

    if (pathname === '/moderation-records') {
      const params: ListRecordsParams = {
        applicationId: search.get('applicationId') ?? undefined,
        status: (search.get('status') as ListRecordsParams['status']) ?? 'all',
        keyword: search.get('keyword') ?? undefined,
        page: Number(search.get('page') ?? '1'),
        pageSize: Number(search.get('pageSize') ?? '8'),
      };
      const filtered = moderationRecords.filter((item) => {
        const matchesApplication =
          !params.applicationId || item.applicationId === params.applicationId;
        const matchesStatus =
          !params.status || params.status === 'all' || item.status === params.status;
        const matchesKeyword =
          !params.keyword ||
          `${item.id} ${item.contentPreview} ${item.category ?? ''}`
            .toLowerCase()
            .includes(params.keyword.toLowerCase());
        return matchesApplication && matchesStatus && matchesKeyword;
      });
      const page = Math.max(params.page ?? 1, 1);
      const pageSize = Math.max(params.pageSize ?? 8, 1);
      const payload: Paginated<ModerationRecord> = {
        items: filtered.slice((page - 1) * pageSize, page * pageSize),
        total: filtered.length,
        page,
        pageSize,
      };
      return result(payload) as T;
    }

    const recordMatch = pathname.match(/^\/moderation-records\/([^/]+)$/);
    if (recordMatch) {
      const record = moderationRecords.find((item) => item.id === recordMatch[1]);
      return record ? (result(record) as T) : notFound('审核记录不存在');
    }

    throw new MockApiError({ code: 'not_found', message: '请求的内容不存在', retryable: false });
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    await wait(320);
    if (path === '/applications') {
      const input = body as CreateApplicationInput;
      const now = new Date().toISOString();
      const application: Application = {
        id: `app-${Date.now()}`,
        name: input.name,
        slug: input.slug,
        description: input.description,
        environment: input.environment,
        status: 'active',
        policyName: input.policyVersion === '2026.08' ? '社区基础策略' : '开放社区策略',
        policyVersion: input.policyVersion,
        createdAt: now,
        lastActiveAt: null,
        totalRequests: 0,
        reviewRate: 0,
        rejectRate: 0,
        activeKeyCount: 0,
      };
      applications.unshift(application);
      return result(application) as T;
    }

    const keyMatch = path.match(/^\/applications\/([^/]+)\/(?:api-keys|keys)$/);
    if (keyMatch) {
      const application = findApplication(keyMatch[1]);
      if (!application) return notFound('应用不存在');
      const input = body as CreateKeyInput;
      if (!input.expiresAt || new Date(input.expiresAt).getTime() <= Date.now()) {
        throw new MockApiError({
          code: 'validation_error',
          message: '请选择未来的到期时间',
          retryable: false,
        });
      }
      const environment = application.environment ?? 'live';
      const plaintext = makePlaintextKey(environment);
      const key: ApiKey = {
        id: `key-${Date.now()}`,
        applicationId: application.id,
        name: input.name,
        prefix: makePrefix(plaintext),
        status: 'active',
        createdAt: new Date().toISOString(),
        expiresAt: input.expiresAt,
        lastUsedAt: null,
        createdBy: '当前用户',
      };
      apiKeys.unshift(key);
      application.activeKeyCount += 1;
      return result({ key, plaintext }) as T;
    }

    const rotateMatch = path.match(/^\/applications\/([^/]+)\/(?:api-keys|keys)\/([^/]+)\/rotate$/);
    if (rotateMatch) {
      const application = findApplication(rotateMatch[1]);
      const oldKey = findKey(rotateMatch[2]);
      if (!application) return notFound('应用不存在');
      if (!oldKey) return notFound('API Key 不存在');
      const input = body as Partial<CreateKeyInput>;
      const expiresAt = input.expiresAt ?? oldKey.expiresAt;
      const plaintext = makePlaintextKey(application.environment ?? 'live');
      const key: ApiKey = {
        id: `key-${Date.now()}`,
        applicationId: application.id,
        name: `${oldKey.name} · 新凭证`,
        prefix: makePrefix(plaintext),
        status: 'active',
        createdAt: new Date().toISOString(),
        expiresAt,
        lastUsedAt: null,
        createdBy: '当前用户',
      };
      apiKeys.unshift(key);
      application.activeKeyCount += 1;
      return result({ key, plaintext }) as T;
    }

    throw new MockApiError({ code: 'not_found', message: '请求的内容不存在', retryable: false });
  }

  async patch<T>(path: string, body?: unknown): Promise<T> {
    await wait(300);
    const applicationMatch = path.match(/^\/applications\/([^/]+)$/);
    if (applicationMatch) {
      const application = findApplication(applicationMatch[1]);
      if (!application) return notFound('应用不存在');
      const requestedStatus = (body as { status?: string }).status;
      const nextStatus: Application['status'] = requestedStatus === 'active' ? 'active' : 'paused';
      if (
        requestedStatus !== 'active' &&
        requestedStatus !== 'paused' &&
        requestedStatus !== 'suspended'
      ) {
        throw new MockApiError({
          code: 'validation_error',
          message: '状态不可用',
          retryable: false,
        });
      }
      application.status = nextStatus;
      application.lastActiveAt =
        nextStatus === 'active' ? new Date().toISOString() : application.lastActiveAt;
      return result(application) as T;
    }

    const keyMatch = path.match(/^\/keys\/([^/]+)$/);
    if (keyMatch) {
      const key = findKey(keyMatch[1]);
      if (!key) return notFound('API Key 不存在');
      const input = body as RevokeKeyInput;
      if (input.reason.trim().length < 4) {
        throw new MockApiError({
          code: 'validation_error',
          message: '请填写撤销原因',
          retryable: false,
        });
      }
      key.status = 'revoked';
      const application = findApplication(key.applicationId);
      if (application && application.activeKeyCount > 0) application.activeKeyCount -= 1;
      return result(key) as T;
    }

    throw new MockApiError({ code: 'not_found', message: '请求的内容不存在', retryable: false });
  }

  async delete<T>(path: string, config?: AxiosRequestConfig): Promise<T> {
    await wait();
    const keyMatch = path.match(/^\/applications\/([^/]+)\/(?:api-keys|keys)\/([^/]+)$/);
    if (keyMatch) {
      const key = findKey(keyMatch[2]);
      if (!key || key.applicationId !== keyMatch[1]) return notFound('API Key 不存在');
      const input = config?.data as RevokeKeyInput | undefined;
      if ((input?.reason ?? '').trim().length < 4) {
        throw new MockApiError({
          code: 'validation_error',
          message: '请填写撤销原因',
          retryable: false,
        });
      }
      key.status = 'revoked';
      const application = findApplication(key.applicationId);
      if (application && application.activeKeyCount > 0) application.activeKeyCount -= 1;
      return undefined as T;
    }
    throw new MockApiError({
      code: 'method_not_allowed',
      message: '当前操作不可用',
      retryable: false,
    });
  }
}
