import { beforeEach, describe, expect, it } from 'vitest';
import { MockApiClient } from './mockAdapter';
import type { ApiKey, OneTimeApiKey } from './types';

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
