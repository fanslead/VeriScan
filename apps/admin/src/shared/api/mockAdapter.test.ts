import { beforeEach, describe, expect, it } from 'vitest';
import { MockApiClient } from './mockAdapter';
import type {
  AiConfiguration,
  ApiKey,
  ApplicationWebhook,
  ApplicationWebhookSaved,
  ApplicationWebhookTest,
  OneTimeApiKey,
} from './types';

describe('MockApiClient API Key lifecycle', () => {
  beforeEach(() => {
    vi.restoreAllMocks();
  });

  it('创建 Key 只返回一次明文，旧 Key 可在切换后单独撤销', async () => {
    const client = new MockApiClient();
    const created = await client.post<OneTimeApiKey>('/applications/app-travel/api-keys', {
      applicationId: 'app-travel',
      name: '测试轮换',
      expiresAt: '2027-09-01T00:00:00Z',
    });
    expect(created.plaintext).toMatch(/^vsk_(live|test)_[0-9a-f]{32}\.[A-Za-z0-9_-]{43}$/);
    expect(created.key.prefix).not.toContain(created.plaintext.slice(-8));

    const beforeRevoke = await client.get<ApiKey[]>('/applications/app-travel/api-keys');
    expect(beforeRevoke.find((key) => key.id === created.key.id)?.status).toBe('active');

    await client.delete<void>(`/applications/app-travel/api-keys/${created.key.id}`, {
      data: { applicationId: 'app-travel', keyId: created.key.id, reason: '测试撤销' },
    });
    const keys = await client.get<ApiKey[]>('/applications/app-travel/api-keys');
    expect(keys.find((key) => key.id === created.key.id)?.status).toBe('revoked');
  });

  it('撤销原因少于四个字时拒绝写入', async () => {
    const client = new MockApiClient();
    await expect(
      client.patch('/keys/key-travel-prod', { keyId: 'key-travel-prod', reason: '短' }),
    ).rejects.toMatchObject({ shape: { code: 'validation_error' } });
  });

  it('没有未来到期时间时不生成凭证', async () => {
    const client = new MockApiClient();
    await expect(
      client.post('/applications/app-travel/api-keys', {
        applicationId: 'app-travel',
        name: '无期限',
        expiresAt: '',
      }),
    ).rejects.toMatchObject({ shape: { code: 'validation_error' } });
  });
});

describe('MockApiClient AI 配置生命周期', () => {
  it('发布前必须通过当前草稿测试，复制版本会重新回到未测试草稿', async () => {
    const client = new MockApiClient();
    const draft = await client.post<AiConfiguration>('/ai/configurations', {
      name: '测试路由',
      protocol: 'openAiChatCompletions',
      baseUrl: 'https://api.openai.com',
      endpointPath: '/v1/chat/completions',
      apiKey: 'sk-test-key',
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
    });

    await expect(client.post(`/ai/configurations/${draft.id}/publish`)).rejects.toMatchObject({
      shape: { code: 'test_required' },
    });

    const test = await client.post(`/ai/configurations/${draft.id}/test`);
    expect(test).toMatchObject({ succeeded: true });
    await client.post(`/ai/configurations/${draft.id}/publish`);
    const revision = await client.post<AiConfiguration>(`/ai/configurations/${draft.id}/revisions`);
    expect(revision).toMatchObject({
      status: 'draft',
      isActive: false,
      lastTestedAt: null,
      lastTestSucceeded: null,
      apiVersionLocation: 'none',
    });
  });
});

describe('MockApiClient Webhook 生命周期', () => {
  it('配置、测试、启用和轮换密钥遵循后端状态约束', async () => {
    const client = new MockApiClient();
    const unconfigured = await client.get<ApplicationWebhook>('/applications/app-travel/webhook');
    expect(unconfigured).toMatchObject({
      configured: false,
      applicationId: 'app-travel',
      enabled: false,
      currentRevisionTested: false,
    });

    const saved = await client.put<ApplicationWebhookSaved>('/applications/app-travel/webhook', {
      endpointUrl: 'https://example.com/veriscan/webhook',
    });
    expect(saved.signingSecret).toMatch(/^whsec_[A-Za-z0-9_-]+$/);
    await expect(
      client.patch('/applications/app-travel/webhook', { enabled: true }),
    ).rejects.toMatchObject({ shape: { code: 'conflict' } });

    const accepted = await client.post<{
      testId: string;
      statusUrl: string;
      submittedAt: string;
    }>('/applications/app-travel/webhook/tests');
    const test = await client.get<ApplicationWebhookTest>(
      `/applications/app-travel/webhook/tests/${accepted.testId}`,
    );
    expect(test).toMatchObject({ status: 'succeeded', httpStatusCode: 200 });
    const enabled = await client.patch<ApplicationWebhook>('/applications/app-travel/webhook', {
      enabled: true,
    });
    expect(enabled.enabled).toBe(true);

    const moved = await client.put<ApplicationWebhookSaved>('/applications/app-travel/webhook', {
      endpointUrl: 'https://example.com/veriscan/moved',
    });
    expect(moved.signingSecret).toMatch(/^whsec_[A-Za-z0-9_-]+$/);
    expect(moved.signingSecret).not.toBe(saved.signingSecret);
    expect(moved.webhook).toMatchObject({
      enabled: false,
      revision: 2,
      currentRevisionTested: false,
    });

    const rotated = await client.post<{ signingSecret: string }>(
      '/applications/app-travel/webhook/secret/rotate',
    );
    expect(rotated.signingSecret).toMatch(/^whsec_[A-Za-z0-9_-]+$/);
    expect(rotated.signingSecret).not.toBe(saved.signingSecret);
    const afterRotate = await client.get<ApplicationWebhook>('/applications/app-travel/webhook');
    expect(afterRotate).toMatchObject({
      enabled: false,
      currentRevisionTested: false,
      lastTestId: null,
    });
  });

  it('拒绝不安全的 Webhook 地址', async () => {
    const client = new MockApiClient();
    await expect(
      client.put('/applications/app-travel/webhook', { endpointUrl: 'http://localhost:8080/hook' }),
    ).rejects.toMatchObject({ shape: { code: 'validation_error' } });
    await expect(
      client.put('/applications/app-travel/webhook', { endpointUrl: 'https://127.0.0.1/hook' }),
    ).rejects.toMatchObject({ shape: { code: 'validation_error' } });
  });
});
