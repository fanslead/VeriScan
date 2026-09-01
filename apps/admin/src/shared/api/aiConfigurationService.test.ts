import { describe, expect, it } from 'vitest';
import type { ApiClient } from './httpClient';
import { createAiConfigurationService } from './services';

const configurationResponse = {
  id: 'ai-config-1',
  publicRevisionId: 'ai-model@1',
  name: '主路由',
  protocol: 'openAiChatCompletions',
  baseUrl: 'https://api.openai.com',
  endpointPath: '/v1/chat/completions',
  credentialRef: 'config://openai-prod',
  authScheme: 'bearer',
  model: 'gpt-4o-mini',
  apiVersion: null,
  apiVersionLocation: 'none',
  systemPrompt: '这是一个用于测试的安全审核系统提示词，长度超过二十个字符。',
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
  createdAt: '2026-09-01T01:00:00Z',
  updatedAt: '2026-09-01T01:00:00Z',
  publishedAt: null,
  lastTestedAt: null,
  lastTestSucceeded: null,
  lastTestFailureCode: null,
};

class RecordingClient implements ApiClient {
  readonly calls: Array<{ method: string; path: string; body?: unknown }> = [];

  async get<T>(path: string): Promise<T> {
    this.calls.push({ method: 'GET', path });
    return { items: [configurationResponse] } as T;
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    this.calls.push({ method: 'POST', path, body });
    if (path.endsWith('/test')) {
      return {
        succeeded: true,
        protocol: 'openAiChatCompletions',
        model: 'gpt-4o-mini',
        latencyMs: 210,
        inputTokens: 18,
        outputTokens: 12,
        failureCode: null,
      } as T;
    }
    return configurationResponse as T;
  }

  async put<T>(path: string, body?: unknown): Promise<T> {
    this.calls.push({ method: 'PUT', path, body });
    return configurationResponse as T;
  }

  async patch<T>(): Promise<T> {
    throw new Error('not used');
  }

  async delete<T>(): Promise<T> {
    throw new Error('not used');
  }
}

const draft = {
  name: '主路由',
  protocol: 'openAiChatCompletions' as const,
  baseUrl: ' https://api.openai.com ',
  endpointPath: ' /v1/chat/completions ',
  credentialRef: ' config://openai-prod ',
  authScheme: 'bearer' as const,
  model: ' gpt-4o-mini ',
  apiVersion: null,
  apiVersionLocation: 'none' as const,
  systemPrompt: '这是一个用于测试的安全审核系统提示词，长度超过二十个字符。',
  decodingMode: 'omitTemperature' as const,
  maxInputTokens: 4096,
  maxOutputTokens: 512,
  connectTimeoutMs: 2000,
  requestTimeoutMs: 15000,
  maxAttempts: 2,
  dataRegion: ' global ',
  retentionClass: '30d',
};

describe('AI 配置服务契约', () => {
  it('使用管理端路由，并在发送前清理草稿文本', async () => {
    const client = new RecordingClient();
    const service = createAiConfigurationService(client);

    await service.list();
    await service.create(draft);
    await service.update('ai-config-1', draft);
    await service.createRevision('ai-config-1');
    await service.test('ai-config-1');
    await service.publish('ai-config-1');
    await service.activate('ai-config-1');
    await service.archive('ai-config-1');

    expect(client.calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      'GET /ai/configurations',
      'POST /ai/configurations',
      'PUT /ai/configurations/ai-config-1',
      'POST /ai/configurations/ai-config-1/revisions',
      'POST /ai/configurations/ai-config-1/test',
      'POST /ai/configurations/ai-config-1/publish',
      'POST /ai/configurations/ai-config-1/activate',
      'POST /ai/configurations/ai-config-1/archive',
    ]);
    expect(client.calls[1].body).toMatchObject({
      baseUrl: 'https://api.openai.com',
      endpointPath: '/v1/chat/completions',
      credentialRef: 'config://openai-prod',
      model: 'gpt-4o-mini',
      apiVersionLocation: 'none',
    });
    expect(client.calls[1].body).not.toHaveProperty('apiKey');
  });
});
