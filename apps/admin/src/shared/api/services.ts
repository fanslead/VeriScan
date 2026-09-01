import { RealApiClient, type ApiClient } from './httpClient';
import {
  mapAiConfigurationListResponse,
  mapAiConfigurationResponse,
  mapAiConfigurationTestResponse,
  mapAiConfigurationDraftInput,
  mapApiKeyListResponse,
  mapApplicationListResponse,
  mapApplicationResponse,
  mapApplicationUsageResponse,
  mapCreatedApiKeyResponse,
  mapModerationRecordListResponse,
  mapModerationRecordResponse,
  mapOverviewResponse,
  mapRuleSetDraftInput,
  mapRuleSetListResponse,
  mapRuleSetResponse,
  mapRuleSetValidationResponse,
} from './realApiAdapter';
import { MockApiClient } from './mockAdapter';
import { useAuthStore } from '@/shared/auth/authStore';
import type {
  AiConfiguration,
  AiConfigurationDraftInput,
  AiConfigurationTestResult,
  AuditEventList,
  ApiKey,
  Application,
  ApplicationUsage,
  CreateApplicationInput,
  CreateKeyInput,
  ListApplicationsParams,
  ListAuditEventsParams,
  ListRecordsParams,
  ModerationRecord,
  OneTimeApiKey,
  OverviewStats,
  Paginated,
  RevokeKeyInput,
  RuleSet,
  RuleSetDraftInput,
  RuleSetValidationResult,
} from './types';

export type ApiMode = 'mock' | 'real';

export const apiMode: ApiMode =
  !import.meta.env.PROD && import.meta.env.VITE_API_MODE === 'mock' ? 'mock' : 'real';
export const apiBaseURL = import.meta.env.VITE_API_BASE_URL?.trim() || '/api/admin/v1';

export const apiClient: ApiClient =
  apiMode === 'mock'
    ? new MockApiClient()
    : new RealApiClient({
        baseURL: apiBaseURL,
        getAccessToken: () => useAuthStore.getState().getAccessToken(),
        onUnauthorized: () => useAuthStore.getState().handleUnauthorized(),
      });

const defaultScopes = ['moderation:submit', 'moderation:read'];

export function createModerationService(client: ApiClient, mode: ApiMode = apiMode) {
  const applicationRequest = (input: CreateApplicationInput) =>
    mode === 'real' ? { name: input.name, environment: input.environment } : input;

  const keyRequest = (input: CreateKeyInput) =>
    mode === 'real'
      ? { displayName: input.name, expiresAt: input.expiresAt, scopes: defaultScopes }
      : input;

  const rotateRequest = (key: ApiKey) =>
    mode === 'real'
      ? {
          displayName: `${key.name} · 新凭证`,
          expiresAt: key.expiresAt,
          revokeOldKey: false,
          scopes: key.scopes?.length ? key.scopes : defaultScopes,
        }
      : key;

  return {
    getOverview: async (): Promise<OverviewStats> =>
      mapOverviewResponse(await client.get<unknown>('/overview')),

    listApplications: async (
      params: ListApplicationsParams = {},
    ): Promise<Paginated<Application>> => {
      const query = new URLSearchParams();
      if (params.keyword) query.set('keyword', params.keyword);
      if (params.status && params.status !== 'all') query.set('status', params.status);
      const suffix = mode === 'mock' && query.toString() ? `?${query.toString()}` : '';
      const result = mapApplicationListResponse(
        await client.get<unknown>(`/applications${suffix}`),
      );
      if (mode === 'real' && (params.keyword || (params.status && params.status !== 'all'))) {
        const keyword = params.keyword?.toLowerCase();
        const items = result.items.filter((application) => {
          const matchesKeyword =
            !keyword || `${application.name} ${application.slug}`.toLowerCase().includes(keyword);
          const matchesStatus =
            !params.status || params.status === 'all' || application.status === params.status;
          return matchesKeyword && matchesStatus;
        });
        return { ...result, items, total: items.length, pageSize: items.length };
      }
      return result;
    },

    getApplication: async (applicationId: string): Promise<Application> =>
      mapApplicationResponse(await client.get<unknown>(`/applications/${applicationId}`)),

    getApplicationUsage: async (applicationId: string): Promise<ApplicationUsage> =>
      mapApplicationUsageResponse(
        await client.get<unknown>(`/applications/${applicationId}/usage`),
      ),

    setApplicationStatus: async (
      applicationId: string,
      status: Application['status'],
    ): Promise<Application> =>
      mapApplicationResponse(
        await client.patch<unknown>(`/applications/${applicationId}`, {
          status: status === 'active' ? 'active' : 'suspended',
        }),
      ),

    createApplication: async (input: CreateApplicationInput): Promise<Application> =>
      mapApplicationResponse(
        await client.post<unknown>('/applications', applicationRequest(input)),
      ),

    bindRuleSet: async (applicationId: string, publicRevisionId: string): Promise<Application> =>
      mapApplicationResponse(
        await client.put<unknown>(`/applications/${applicationId}/rule-set`, {
          publicRevisionId,
        }),
      ),

    listKeys: async (applicationId: string): Promise<ApiKey[]> =>
      mapApiKeyListResponse(
        await client.get<unknown>(`/applications/${applicationId}/api-keys`),
        applicationId,
      ),

    createKey: async (input: CreateKeyInput): Promise<OneTimeApiKey> =>
      mapCreatedApiKeyResponse(
        await client.post<unknown>(
          `/applications/${input.applicationId}/api-keys`,
          keyRequest(input),
        ),
        { applicationId: input.applicationId, name: input.name },
      ),

    rotateKey: async (key: ApiKey): Promise<OneTimeApiKey> =>
      mapCreatedApiKeyResponse(
        await client.post<unknown>(
          `/applications/${key.applicationId}/api-keys/${key.id}/rotate`,
          rotateRequest(key),
        ),
        { applicationId: key.applicationId, name: `${key.name} · 新凭证` },
      ),

    revokeKey: async (input: RevokeKeyInput): Promise<void> => {
      await client.delete<void>(
        `/applications/${input.applicationId}/api-keys/${input.keyId}`,
        mode === 'mock' ? { data: input } : undefined,
      );
    },

    listRecords: async (params: ListRecordsParams = {}): Promise<Paginated<ModerationRecord>> => {
      const query = new URLSearchParams();
      if (params.applicationId) query.set('applicationId', params.applicationId);
      if (params.status && params.status !== 'all') query.set('status', params.status);
      if (params.keyword) query.set('keyword', params.keyword);
      query.set('page', String(params.page ?? 1));
      query.set('pageSize', String(params.pageSize ?? 8));
      return mapModerationRecordListResponse(
        await client.get<unknown>(`/moderation-records?${query.toString()}`),
      );
    },

    getRecord: async (recordId: string): Promise<ModerationRecord> =>
      mapModerationRecordResponse(await client.get<unknown>(`/moderation-records/${recordId}`)),
  };
}

export const moderationService = createModerationService(apiClient);

export function createAiConfigurationService(client: ApiClient) {
  return {
    list: async (): Promise<AiConfiguration[]> =>
      mapAiConfigurationListResponse(await client.get<unknown>('/ai/configurations')),

    get: async (configurationId: string): Promise<AiConfiguration> =>
      mapAiConfigurationResponse(
        await client.get<unknown>(`/ai/configurations/${configurationId}`),
      ),

    create: async (input: AiConfigurationDraftInput): Promise<AiConfiguration> =>
      mapAiConfigurationResponse(
        await client.post<unknown>('/ai/configurations', mapAiConfigurationDraftInput(input)),
      ),

    update: async (
      configurationId: string,
      input: AiConfigurationDraftInput,
    ): Promise<AiConfiguration> =>
      mapAiConfigurationResponse(
        await client.put<unknown>(
          `/ai/configurations/${configurationId}`,
          mapAiConfigurationDraftInput(input),
        ),
      ),

    createRevision: async (configurationId: string): Promise<AiConfiguration> =>
      mapAiConfigurationResponse(
        await client.post<unknown>(`/ai/configurations/${configurationId}/revisions`),
      ),

    test: async (configurationId: string): Promise<AiConfigurationTestResult> =>
      mapAiConfigurationTestResponse(
        await client.post<unknown>(`/ai/configurations/${configurationId}/test`),
      ),

    publish: async (configurationId: string): Promise<AiConfiguration> =>
      mapAiConfigurationResponse(
        await client.post<unknown>(`/ai/configurations/${configurationId}/publish`),
      ),

    activate: async (configurationId: string): Promise<AiConfiguration> =>
      mapAiConfigurationResponse(
        await client.post<unknown>(`/ai/configurations/${configurationId}/activate`),
      ),

    archive: async (configurationId: string): Promise<AiConfiguration> =>
      mapAiConfigurationResponse(
        await client.post<unknown>(`/ai/configurations/${configurationId}/archive`),
      ),
  };
}

export const aiConfigurationService = createAiConfigurationService(apiClient);

export function createRuleSetService(client: ApiClient) {
  return {
    list: async (): Promise<RuleSet[]> =>
      mapRuleSetListResponse(await client.get<unknown>('/rule-sets')),

    get: async (ruleSetId: string): Promise<RuleSet> =>
      mapRuleSetResponse(await client.get<unknown>(`/rule-sets/${ruleSetId}`)),

    create: async (input: RuleSetDraftInput): Promise<RuleSet> =>
      mapRuleSetResponse(await client.post<unknown>('/rule-sets', mapRuleSetDraftInput(input))),

    update: async (ruleSetId: string, input: RuleSetDraftInput): Promise<RuleSet> =>
      mapRuleSetResponse(
        await client.put<unknown>(`/rule-sets/${ruleSetId}`, mapRuleSetDraftInput(input)),
      ),

    createRevision: async (ruleSetId: string): Promise<RuleSet> =>
      mapRuleSetResponse(await client.post<unknown>(`/rule-sets/${ruleSetId}/revisions`)),

    validate: async (ruleSetId: string): Promise<RuleSetValidationResult> =>
      mapRuleSetValidationResponse(await client.post<unknown>(`/rule-sets/${ruleSetId}/validate`)),

    publish: async (ruleSetId: string): Promise<RuleSet> =>
      mapRuleSetResponse(await client.post<unknown>(`/rule-sets/${ruleSetId}/publish`)),

    archive: async (ruleSetId: string): Promise<RuleSet> =>
      mapRuleSetResponse(await client.post<unknown>(`/rule-sets/${ruleSetId}/archive`)),
  };
}

export const ruleSetService = createRuleSetService(apiClient);

export function createAuditService(client: ApiClient) {
  return {
    list: async (params: ListAuditEventsParams = {}): Promise<AuditEventList> => {
      const query = new URLSearchParams();
      if (params.applicationId) query.set('applicationId', params.applicationId);
      if (params.action) query.set('action', params.action);
      if (params.from) query.set('from', params.from);
      if (params.through) query.set('through', params.through);
      query.set('limit', String(params.limit ?? 100));
      return client.get<AuditEventList>(`/audit-events?${query.toString()}`);
    },
  };
}

export const auditService = createAuditService(apiClient);
