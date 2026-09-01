import { RealApiClient, type ApiClient } from './httpClient';
import {
  mapApiKeyListResponse,
  mapApplicationListResponse,
  mapApplicationResponse,
  mapCreatedApiKeyResponse,
  mapModerationRecordListResponse,
  mapModerationRecordResponse,
  mapOverviewResponse,
} from './realApiAdapter';
import { MockApiClient } from './mockAdapter';
import { useAuthStore } from '@/shared/auth/authStore';
import type {
  ApiKey,
  Application,
  CreateApplicationInput,
  CreateKeyInput,
  ListApplicationsParams,
  ListRecordsParams,
  ModerationRecord,
  OneTimeApiKey,
  OverviewStats,
  Paginated,
  RevokeKeyInput,
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
