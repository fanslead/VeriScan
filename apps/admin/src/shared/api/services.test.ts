import { describe, expect, it } from 'vitest';
import type { AxiosRequestConfig } from 'axios';
import { createModerationService } from './services';
import type { ApiClient } from './httpClient';
import type { ApiKey } from './types';

class RecordingClient implements ApiClient {
  readonly calls: Array<{
    method: string;
    path: string;
    body?: unknown;
    config?: AxiosRequestConfig;
  }> = [];

  async get<T>(path: string): Promise<T> {
    this.calls.push({ method: 'GET', path });
    if (path === '/applications/app-1/usage') {
      return {
        applicationId: 'app-1',
        apiKeyId: null,
        dataFrom: '2026-08-25T00:00:00Z',
        dataThrough: '2026-09-01T00:00:00Z',
        requestCount: 8,
        itemCount: 10,
        passCount: 7,
        rejectCount: 2,
        reviewCount: 1,
        aiCallCount: 3,
        aiInputTokens: 120,
        aiOutputTokens: 30,
        aiFailureCount: 0,
      } as T;
    }
    if (path === '/applications/app-1/api-keys') {
      return {
        items: [
          {
            keyId: 'key-1',
            keyPrefix: 'vsk_live_abc',
            lastFour: '1234',
            scopes: ['moderation:submit'],
            environment: 'live',
            status: 'active',
            notBefore: '2026-09-01T00:00:00Z',
            expiresAt: '2027-09-01T00:00:00Z',
            createdAt: '2026-09-01T00:00:00Z',
            revokedAt: null,
            lastUsedAt: null,
            displayName: '生产服务',
          },
        ],
        totalCount: 1,
      } as T;
    }
    return { items: [], totalCount: 0 } as T;
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    this.calls.push({ method: 'POST', path, body });
    if (path === '/applications') {
      return {
        id: 'app-1',
        publicId: 'app_public_1',
        name: '星河电商社区',
        environment: 'live',
        status: 'active',
        activeKeyCount: 0,
        createdAt: '2026-09-01T00:00:00Z',
        updatedAt: '2026-09-01T00:00:00Z',
      } as T;
    }
    return {
      keyId: 'key-new',
      keyPrefix: 'vsk_live_abc',
      apiKey: 'vsk_live_abc.abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_',
      scopes: ['moderation:submit', 'moderation:read'],
      expiresAt: '2027-09-01T00:00:00Z',
    } as T;
  }

  async put<T>(path: string, body?: unknown): Promise<T> {
    this.calls.push({ method: 'PUT', path, body });
    return {
      id: 'ai-config-1',
      publicRevisionId: 'ai-model@2026-09-01',
      name: 'AI 配置',
      protocol: 'openAiChatCompletions',
      baseUrl: 'https://api.openai.com',
      endpointPath: '/v1/chat/completions',
      credentialRef: 'config://moderation',
      hasCredential: true,
      credentialSource: 'server',
      authScheme: 'bearer',
      model: 'gpt-4o-mini',
      systemPrompt: '这是一个至少二十个字符的测试系统提示词。',
      decodingMode: 'omitTemperature',
      maxInputTokens: 4096,
      maxOutputTokens: 512,
      connectTimeoutMs: 2000,
      requestTimeoutMs: 15000,
      maxAttempts: 2,
      dataRegion: 'global',
      retentionClass: '30d',
      status: 'draft',
      isActive: false,
      createdAt: '2026-09-01T00:00:00Z',
      updatedAt: '2026-09-01T00:00:00Z',
      publishedAt: null,
    } as T;
  }

  async patch<T>(path: string, body?: unknown): Promise<T> {
    this.calls.push({ method: 'PATCH', path, body });
    return {
      id: 'app-1',
      publicId: 'app_public_1',
      name: '星河电商社区',
      environment: 'live',
      status: 'suspended',
      activeKeyCount: 0,
      createdAt: '2026-09-01T00:00:00Z',
      updatedAt: '2026-09-01T00:00:00Z',
    } as T;
  }

  async delete<T>(path: string, config?: AxiosRequestConfig): Promise<T> {
    this.calls.push({ method: 'DELETE', path, config });
    return undefined as T;
  }
}

const key: ApiKey = {
  id: 'key-1',
  applicationId: 'app-1',
  name: '生产服务',
  prefix: 'vsk_live_abc',
  status: 'active',
  createdAt: '2026-09-01T00:00:00Z',
  expiresAt: '2027-09-01T00:00:00Z',
  lastUsedAt: null,
  createdBy: '管理后台',
  scopes: ['moderation:submit'],
};

describe('真实 API service contract', () => {
  it('创建应用只发送后端契约字段并包含环境', async () => {
    const client = new RecordingClient();
    const service = createModerationService(client, 'real');

    await service.createApplication({
      name: '星河电商社区',
      slug: 'ignored-by-api',
      description: '仅用于展示',
      environment: 'live',
      policyVersion: '2026.08',
    });

    expect(client.calls[0]).toMatchObject({
      method: 'POST',
      path: '/applications',
      body: { name: '星河电商社区', environment: 'live' },
    });
  });

  it('使用应用级 API Key 路径，并保留轮换重叠窗口', async () => {
    const client = new RecordingClient();
    const service = createModerationService(client, 'real');

    await service.listKeys('app-1');
    await service.createKey({
      applicationId: 'app-1',
      name: '生产服务',
      expiresAt: '2027-09-01T00:00:00Z',
    });
    await service.rotateKey(key);
    await service.revokeKey({ applicationId: 'app-1', keyId: 'key-1', reason: '已完成切换' });

    expect(client.calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      'GET /applications/app-1/api-keys',
      'POST /applications/app-1/api-keys',
      'POST /applications/app-1/api-keys/key-1/rotate',
      'DELETE /applications/app-1/api-keys/key-1',
    ]);
    expect(client.calls[1].body).toEqual({
      displayName: '生产服务',
      expiresAt: '2027-09-01T00:00:00Z',
      scopes: ['moderation:submit', 'moderation:read'],
    });
    expect(client.calls[2].body).toMatchObject({
      displayName: '生产服务 · 新凭证',
      expiresAt: '2027-09-01T00:00:00Z',
      revokeOldKey: false,
      scopes: ['moderation:submit'],
    });
    expect(client.calls[3].config).toBeUndefined();
  });

  it('使用独立用量端点读取应用事实统计', async () => {
    const client = new RecordingClient();
    const service = createModerationService(client, 'real');

    const usage = await service.getApplicationUsage('app-1');

    expect(client.calls[0]).toMatchObject({
      method: 'GET',
      path: '/applications/app-1/usage',
    });
    expect(usage).toMatchObject({
      applicationId: 'app-1',
      requestCount: 8,
      itemCount: 10,
      reviewCount: 1,
      aiInputTokens: 120,
    });
  });
});
