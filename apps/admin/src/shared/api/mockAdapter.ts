import type { AxiosRequestConfig } from 'axios';
import type { ApiClient } from './httpClient';
import {
  aiConfigurations,
  apiKeys,
  applications,
  moderationRecords,
  overviewStats,
  ruleSets,
  auditEvents,
} from './mockData';
import type {
  AiConfiguration,
  AiConfigurationDraftInput,
  ApiErrorShape,
  ApiKey,
  Application,
  ApplicationUsage,
  CreateApplicationInput,
  CreateKeyInput,
  ListApplicationsParams,
  ListRecordsParams,
  ModerationRecord,
  Paginated,
  RevokeKeyInput,
  RuleSet,
  RuleSetDraftInput,
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

const findAiConfiguration = (id: string) => aiConfigurations.find((item) => item.id === id);

const findRuleSet = (id: string) => ruleSets.find((item) => item.id === id);

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

    if (pathname === '/audit-events') {
      const action = search.get('action');
      const applicationId = search.get('applicationId');
      const limit = Math.max(Number(search.get('limit') ?? '100'), 1);
      const items = auditEvents
        .filter((event) => !action || event.action === action)
        .filter((event) => !applicationId || event.applicationId === applicationId)
        .slice(0, limit);
      return result({
        items,
        total: items.length,
        dataFrom: items.at(-1)?.occurredAt ?? new Date().toISOString(),
        dataThrough: items[0]?.occurredAt ?? new Date().toISOString(),
      }) as T;
    }

    if (pathname === '/ai/configurations') {
      return result({ items: aiConfigurations }) as T;
    }

    if (pathname === '/rule-sets') {
      return result({ items: ruleSets }) as T;
    }

    const ruleSetMatch = pathname.match(/^\/rule-sets\/([^/]+)$/);
    if (ruleSetMatch) {
      const ruleSet = findRuleSet(ruleSetMatch[1]);
      return ruleSet ? (result(ruleSet) as T) : notFound('规则集不存在');
    }

    const aiConfigurationMatch = pathname.match(/^\/ai\/configurations\/([^/]+)$/);
    if (aiConfigurationMatch) {
      const configuration = findAiConfiguration(aiConfigurationMatch[1]);
      return configuration ? (result(configuration) as T) : notFound('AI 配置不存在');
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

    const usageMatch = pathname.match(/^\/applications\/([^/]+)\/usage$/);
    if (usageMatch) {
      const application = findApplication(usageMatch[1]);
      if (!application) return notFound('应用不存在');
      const itemCount = application.totalRequests ?? 0;
      const rejectCount = Math.round(itemCount * ((application.rejectRate ?? 0) / 100));
      const reviewCount = Math.round(itemCount * ((application.reviewRate ?? 0) / 100));
      const now = new Date();
      const usage: ApplicationUsage = {
        applicationId: application.id,
        apiKeyId: null,
        dataFrom: new Date(now.getTime() - 7 * 24 * 60 * 60 * 1000).toISOString(),
        dataThrough: now.toISOString(),
        requestCount: itemCount,
        itemCount,
        passCount: Math.max(0, itemCount - rejectCount - reviewCount),
        rejectCount,
        reviewCount,
        aiCallCount: reviewCount,
        aiInputTokens: reviewCount * 96,
        aiOutputTokens: reviewCount * 24,
        aiFailureCount: Math.round(reviewCount * 0.002),
      };
      return result(usage) as T;
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
    if (path === '/rule-sets') {
      const input = body as RuleSetDraftInput;
      const now = new Date().toISOString();
      const id = `rules-${Date.now()}`;
      const ruleSet: RuleSet = {
        id,
        publicRevisionId: `ruleset@${Date.now()}`,
        name: input.name,
        status: 'draft',
        ruleCount: input.rules.length + input.regexRules.length + input.combinationRules.length,
        rulesTruncated: false,
        createdAt: now,
        updatedAt: now,
        lastValidatedAt: null,
        lastValidatedChecksum: null,
        publishedAt: null,
        publishedChecksum: null,
        applicationCount: 0,
        normalizationProfile: input.normalizationProfile,
        regexRules: input.regexRules.map((rule, index) => ({
          ...rule,
          id: `${id}-regex-${index}`,
          isEnabled: true,
        })),
        combinationRules: input.combinationRules.map((rule, index) => ({
          ...rule,
          id: `${id}-combination-${index}`,
          isEnabled: true,
        })),
        rules: input.rules.map((rule, index) => ({
          ...rule,
          id: `${id}-word-${index}`,
          isEnabled: true,
        })),
      };
      ruleSets.unshift(ruleSet);
      return result(ruleSet) as T;
    }

    const ruleValidationMatch = path.match(/^\/rule-sets\/([^/]+)\/validate$/);
    if (ruleValidationMatch) {
      const ruleSet = findRuleSet(ruleValidationMatch[1]);
      if (!ruleSet) return notFound('规则集不存在');
      const checksum = `${ruleSet.id.replace(/[^a-z0-9]/gi, 'a').padEnd(64, '0')}`.slice(0, 64);
      ruleSet.lastValidatedAt = new Date().toISOString();
      ruleSet.lastValidatedChecksum = checksum;
      return result({ valid: true, checksum, ruleCount: ruleSet.ruleCount, issues: [] }) as T;
    }

    const ruleRevisionMatch = path.match(/^\/rule-sets\/([^/]+)\/revisions$/);
    if (ruleRevisionMatch) {
      const source = findRuleSet(ruleRevisionMatch[1]);
      if (!source) return notFound('规则集不存在');
      const now = new Date().toISOString();
      const revision: RuleSet = {
        ...structuredClone(source),
        id: `rules-${Date.now()}`,
        publicRevisionId: `ruleset@${Date.now()}`,
        name: `${source.name} · 新版本`,
        status: 'draft',
        rulesTruncated: false,
        createdAt: now,
        updatedAt: now,
        lastValidatedAt: null,
        lastValidatedChecksum: null,
        publishedAt: null,
        publishedChecksum: null,
        applicationCount: 0,
      };
      ruleSets.unshift(revision);
      return result(revision) as T;
    }

    const ruleLifecycleMatch = path.match(/^\/rule-sets\/([^/]+)\/(publish|archive)$/);
    if (ruleLifecycleMatch) {
      const ruleSet = findRuleSet(ruleLifecycleMatch[1]);
      if (!ruleSet) return notFound('规则集不存在');
      if (ruleLifecycleMatch[2] === 'publish') {
        if (!ruleSet.lastValidatedChecksum) {
          throw new MockApiError({
            code: 'conflict',
            message: '请先完成规则校验',
            retryable: false,
          });
        }
        ruleSet.status = 'published';
        ruleSet.publishedAt = new Date().toISOString();
        ruleSet.publishedChecksum = ruleSet.lastValidatedChecksum;
      } else {
        if (ruleSet.applicationCount > 0) {
          throw new MockApiError({
            code: 'conflict',
            message: '仍有应用绑定该版本',
            retryable: false,
          });
        }
        ruleSet.status = 'archived';
      }
      ruleSet.updatedAt = new Date().toISOString();
      return result(ruleSet) as T;
    }

    if (path === '/ai/configurations') {
      const input = body as AiConfigurationDraftInput;
      const { apiKey, ...safeInput } = structuredClone(input);
      const now = new Date().toISOString();
      const configuration: AiConfiguration = {
        ...safeInput,
        id: `ai-config-${Date.now()}`,
        publicRevisionId: `ai-model@${Date.now()}`,
        credentialRef: 'managed://encrypted',
        hasCredential: Boolean(apiKey),
        credentialSource: 'managed',
        status: 'draft',
        isActive: false,
        createdAt: now,
        updatedAt: now,
        publishedAt: null,
        lastTestedAt: null,
        lastTestSucceeded: null,
        lastTestFailureCode: null,
        adapterContractVersion: null,
        canonicalSchemaVersion: null,
        canonicalSchemaHash: null,
        effectiveSchemaHash: null,
        schemaTransformerVersion: null,
      };
      aiConfigurations.unshift(configuration);
      return result(configuration) as T;
    }

    const aiTestMatch = path.match(/^\/ai\/configurations\/([^/]+)\/test$/);
    if (aiTestMatch) {
      const configuration = findAiConfiguration(aiTestMatch[1]);
      if (!configuration) return notFound('AI 配置不存在');
      if (configuration.status === 'archived') {
        throw new MockApiError({
          code: 'conflict',
          message: '已归档的配置不能执行连接测试',
          retryable: false,
        });
      }
      const testedAt = new Date().toISOString();
      configuration.lastTestedAt = testedAt;
      configuration.lastTestSucceeded = true;
      configuration.lastTestFailureCode = null;
      return result({
        succeeded: true,
        protocol: configuration.protocol,
        model: configuration.model,
        latencyMs: 184,
        inputTokens: 42,
        outputTokens: 18,
        failureCode: null,
      }) as T;
    }

    const aiRevisionMatch = path.match(/^\/ai\/configurations\/([^/]+)\/revisions$/);
    if (aiRevisionMatch) {
      const source = findAiConfiguration(aiRevisionMatch[1]);
      if (!source) return notFound('AI 配置不存在');
      const now = new Date().toISOString();
      const revision: AiConfiguration = {
        ...structuredClone(source),
        id: `ai-config-${Date.now()}`,
        publicRevisionId: `ai-model@${Date.now()}`,
        name: `${source.name} · 新版本`,
        status: 'draft',
        isActive: false,
        createdAt: now,
        updatedAt: now,
        publishedAt: null,
        lastTestedAt: null,
        lastTestSucceeded: null,
        lastTestFailureCode: null,
        adapterContractVersion: null,
        canonicalSchemaVersion: null,
        canonicalSchemaHash: null,
        effectiveSchemaHash: null,
        schemaTransformerVersion: null,
      };
      aiConfigurations.unshift(revision);
      return result(revision) as T;
    }

    const aiLifecycleMatch = path.match(
      /^\/ai\/configurations\/([^/]+)\/(publish|activate|archive)$/,
    );
    if (aiLifecycleMatch) {
      const configuration = findAiConfiguration(aiLifecycleMatch[1]);
      if (!configuration) return notFound('AI 配置不存在');
      const action = aiLifecycleMatch[2];
      const now = new Date().toISOString();
      if (action === 'publish') {
        if (configuration.status !== 'draft') {
          throw new MockApiError({
            code: 'conflict',
            message: '只有草稿可以发布',
            retryable: false,
          });
        }
        if (
          configuration.lastTestSucceeded !== true ||
          !configuration.lastTestedAt ||
          Date.parse(configuration.lastTestedAt) < Date.parse(configuration.updatedAt)
        ) {
          throw new MockApiError({
            code: 'test_required',
            message: '请先通过当前草稿的合成测试',
            retryable: false,
          });
        }
        configuration.status = 'published';
        configuration.publishedAt = now;
      } else if (action === 'activate') {
        if (configuration.status !== 'published') {
          throw new MockApiError({
            code: 'conflict',
            message: '只有已发布配置可以激活',
            retryable: false,
          });
        }
        aiConfigurations.forEach((item) => {
          item.isActive = item.id === configuration.id;
        });
      } else {
        configuration.status = 'archived';
        configuration.isActive = false;
      }
      configuration.updatedAt = now;
      return result(configuration) as T;
    }

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

  async put<T>(path: string, body?: unknown): Promise<T> {
    await wait(300);
    const bindingMatch = path.match(/^\/applications\/([^/]+)\/rule-set$/);
    if (bindingMatch) {
      const application = findApplication(bindingMatch[1]);
      if (!application) return notFound('应用不存在');
      const revisionId = (body as { publicRevisionId?: string }).publicRevisionId;
      const next = ruleSets.find((item) => item.publicRevisionId === revisionId);
      if (!next || next.status !== 'published') return notFound('已发布规则集不存在');
      const previous = ruleSets.find((item) => item.publicRevisionId === application.policyVersion);
      if (previous) previous.applicationCount = Math.max(0, previous.applicationCount - 1);
      next.applicationCount += 1;
      application.policyName = next.name;
      application.policyVersion = next.publicRevisionId;
      return result(application) as T;
    }

    const ruleSetMatch = path.match(/^\/rule-sets\/([^/]+)$/);
    if (ruleSetMatch) {
      const ruleSet = findRuleSet(ruleSetMatch[1]);
      if (!ruleSet) return notFound('规则集不存在');
      if (ruleSet.status !== 'draft') {
        throw new MockApiError({ code: 'conflict', message: '发布版本不可修改', retryable: false });
      }
      const input = body as RuleSetDraftInput;
      ruleSet.name = input.name;
      ruleSet.rules = input.rules.map((rule, index) => ({
        ...rule,
        id: `${ruleSet.id}-word-${index}`,
        isEnabled: true,
      }));
      ruleSet.normalizationProfile = input.normalizationProfile;
      ruleSet.regexRules = input.regexRules.map((rule, index) => ({
        ...rule,
        id: `${ruleSet.id}-regex-${index}`,
        isEnabled: true,
      }));
      ruleSet.combinationRules = input.combinationRules.map((rule, index) => ({
        ...rule,
        id: `${ruleSet.id}-combination-${index}`,
        isEnabled: true,
      }));
      ruleSet.ruleCount =
        ruleSet.rules.length + ruleSet.regexRules.length + ruleSet.combinationRules.length;
      ruleSet.updatedAt = new Date().toISOString();
      ruleSet.lastValidatedAt = null;
      ruleSet.lastValidatedChecksum = null;
      return result(ruleSet) as T;
    }

    const aiMatch = path.match(/^\/ai\/configurations\/([^/]+)$/);
    if (aiMatch) {
      const configuration = findAiConfiguration(aiMatch[1]);
      if (!configuration) return notFound('AI 配置不存在');
      if (configuration.status !== 'draft') {
        throw new MockApiError({
          code: 'conflict',
          message: '已发布或已归档的配置不可修改',
          retryable: false,
        });
      }
      const input = body as AiConfigurationDraftInput;
      const { apiKey, ...safeInput } = structuredClone(input);
      Object.assign(configuration, safeInput, {
        hasCredential: configuration.hasCredential || Boolean(apiKey),
        credentialSource: apiKey ? 'managed' : configuration.credentialSource,
        updatedAt: new Date().toISOString(),
        lastTestedAt: null,
        lastTestSucceeded: null,
        lastTestFailureCode: null,
        adapterContractVersion: null,
        canonicalSchemaVersion: null,
        canonicalSchemaHash: null,
        effectiveSchemaHash: null,
        schemaTransformerVersion: null,
      });
      return result(configuration) as T;
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
