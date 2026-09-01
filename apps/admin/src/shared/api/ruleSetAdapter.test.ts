import { describe, expect, it } from 'vitest';
import type { ApiClient } from './httpClient';
import { mapRuleSetResponse } from './ruleSetAdapter';
import { createRuleSetService } from './services';

const response = {
  id: 'rules-1',
  publicRevisionId: 'ruleset@1',
  name: '基础规则',
  status: 'published',
  ruleCount: 1,
  rulesTruncated: false,
  createdAt: '2026-09-01T01:00:00Z',
  updatedAt: '2026-09-01T01:00:00Z',
  lastValidatedAt: '2026-09-01T01:00:00Z',
  lastValidatedChecksum: 'abc',
  publishedAt: '2026-09-01T01:00:00Z',
  publishedChecksum: 'abc',
  applicationCount: 2,
  normalizationProfile: 'traditionalSimplified',
  regexRules: [
    {
      id: 'regex-1',
      pattern: 'https?://[^\\s]+',
      action: 'forceReview',
      category: 'contact',
      weight: 0.8,
      timeoutMs: 100,
      maxInputLength: 65536,
      engineMode: 'nonBacktracking',
      priority: 0,
      isEnabled: true,
    },
  ],
  combinationRules: [
    {
      id: 'combination-1',
      name: '站外导流',
      terms: ['优惠', '加微信'],
      action: 'riskSignal',
      category: 'contact',
      weight: 0.6,
      windowSize: 64,
      priority: 0,
      isEnabled: true,
    },
  ],
  rules: [
    {
      id: 'word-1',
      term: '赌博',
      type: 'black',
      category: 'gambling',
      weight: 1,
      isEnabled: true,
    },
  ],
};

class RecordingClient implements ApiClient {
  readonly calls: Array<{ method: string; path: string; body?: unknown }> = [];

  async get<T>(path: string): Promise<T> {
    this.calls.push({ method: 'GET', path });
    return (path === '/rule-sets' ? { items: [response] } : response) as T;
  }

  async post<T>(path: string, body?: unknown): Promise<T> {
    this.calls.push({ method: 'POST', path, body });
    if (path.endsWith('/validate')) {
      return { valid: true, checksum: 'abc', ruleCount: 1, issues: [] } as T;
    }
    return response as T;
  }

  async put<T>(path: string, body?: unknown): Promise<T> {
    this.calls.push({ method: 'PUT', path, body });
    return response as T;
  }

  async patch<T>(): Promise<T> {
    throw new Error('not used');
  }

  async delete<T>(): Promise<T> {
    throw new Error('not used');
  }
}

describe('规则集 API 契约', () => {
  it('严格映射规则与生命周期事实', () => {
    const ruleSet = mapRuleSetResponse(response);
    expect(ruleSet.status).toBe('published');
    expect(ruleSet.applicationCount).toBe(2);
    expect(ruleSet.rules[0]).toMatchObject({ type: 'black', term: '赌博', weight: 1 });
  });

  it('使用管理端版本路由并清理草稿文本', async () => {
    const client = new RecordingClient();
    const service = createRuleSetService(client);
    const input = {
      name: ' 基础规则 ',
      rules: [{ term: ' 赌博 ', type: 'black' as const, category: ' GAMBLING ', weight: 1 }],
      normalizationProfile: 'traditionalSimplified' as const,
      regexRules: [],
      combinationRules: [],
    };

    await service.list();
    await service.create(input);
    await service.update('rules-1', input);
    await service.createRevision('rules-1');
    await service.validate('rules-1');
    await service.publish('rules-1');
    await service.archive('rules-1');

    expect(client.calls.map((call) => `${call.method} ${call.path}`)).toEqual([
      'GET /rule-sets',
      'POST /rule-sets',
      'PUT /rule-sets/rules-1',
      'POST /rule-sets/rules-1/revisions',
      'POST /rule-sets/rules-1/validate',
      'POST /rule-sets/rules-1/publish',
      'POST /rule-sets/rules-1/archive',
    ]);
    expect(client.calls[1].body).toEqual({
      name: '基础规则',
      rules: [
        {
          term: '赌博',
          type: 'black',
          category: 'gambling',
          weight: 1,
          language: null,
          scene: null,
          evidenceTemplate: null,
          source: null,
        },
      ],
      normalizationProfile: 'traditionalSimplified',
      regexRules: [],
      combinationRules: [],
    });
  });
});
