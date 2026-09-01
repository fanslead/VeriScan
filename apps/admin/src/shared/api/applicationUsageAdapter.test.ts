import { describe, expect, it } from 'vitest';
import { mapApplicationUsageResponse } from './applicationUsageAdapter';

describe('application usage adapter', () => {
  it('保留服务端事实并让缺失 Token 保持 null', () => {
    expect(
      mapApplicationUsageResponse({
        applicationId: 'app-1',
        apiKeyId: null,
        dataFrom: '2026-08-25T00:00:00Z',
        dataThrough: '2026-09-01T00:00:00Z',
        requestCount: 12,
        itemCount: 20,
        passCount: 14,
        rejectCount: 3,
        reviewCount: 3,
        aiCallCount: 5,
        aiInputTokens: null,
        aiOutputTokens: 48,
        aiFailureCount: 1,
      }),
    ).toMatchObject({
      applicationId: 'app-1',
      requestCount: 12,
      itemCount: 20,
      aiInputTokens: null,
      aiOutputTokens: 48,
      aiFailureCount: 1,
    });
  });

  it('不会把非法或负数统计映射成看似有效的数据', () => {
    const result = mapApplicationUsageResponse({
      requestCount: -1,
      itemCount: 'invalid',
      aiInputTokens: -3,
    });

    expect(result.requestCount).toBe(0);
    expect(result.itemCount).toBe(0);
    expect(result.aiInputTokens).toBeNull();
    expect(result.dataThrough).toBe('');
  });
});
