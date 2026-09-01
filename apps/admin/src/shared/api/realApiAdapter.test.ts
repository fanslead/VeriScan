import { describe, expect, it } from 'vitest';
import {
  mapApiKeyListResponse,
  mapApplicationListResponse,
  mapApplicationResponse,
  mapCreatedApiKeyResponse,
  mapModerationRecordResponse,
  mapOverviewResponse,
} from './realApiAdapter';

describe('管理 API DTO 适配', () => {
  it('将应用响应映射为页面模型，不伪造未返回的统计与策略', () => {
    const application = mapApplicationResponse({
      id: '4fdd3ec4-9f20-4d43-b0b1-b3c4ac9c7b9d',
      publicId: 'app_public_1',
      name: '星河电商社区',
      environment: 'live',
      status: 'active',
      activeKeyCount: 2,
      createdAt: '2026-09-01T00:00:00Z',
      updatedAt: '2026-09-01T01:00:00Z',
    });

    expect(application).toMatchObject({
      id: '4fdd3ec4-9f20-4d43-b0b1-b3c4ac9c7b9d',
      slug: 'app_public_1',
      environment: 'live',
      activeKeyCount: 2,
      totalRequests: null,
      rejectRate: null,
      reviewRate: null,
      policyName: null,
      policyVersion: null,
      lastActiveAt: null,
    });
  });

  it('映射后端返回的应用规则绑定', () => {
    const application = mapApplicationResponse({
      id: 'app-1',
      publicId: 'app_public_1',
      name: '策略应用',
      environment: 'test',
      status: 'active',
      activeKeyCount: 1,
      ruleSetRevisionId: 'ruleset@42',
      ruleSetName: '社区规则',
      createdAt: '2026-09-01T00:00:00Z',
      updatedAt: '2026-09-01T01:00:00Z',
    });

    expect(application.policyVersion).toBe('ruleset@42');
    expect(application.policyName).toBe('社区规则');
  });

  it('兼容 Key DisplayName，并保留一次性明文', () => {
    const keys = mapApiKeyListResponse(
      {
        items: [
          {
            keyId: 'key-1',
            displayName: '生产服务',
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
          },
        ],
        totalCount: 1,
      },
      'app-1',
    );
    const created = mapCreatedApiKeyResponse(
      {
        keyId: 'key-2',
        displayName: '轮换凭证',
        keyPrefix: 'vsk_live_def',
        apiKey: 'vsk_live_def.abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789-_',
        scopes: ['moderation:submit', 'moderation:read'],
        expiresAt: '2027-09-01T00:00:00Z',
      },
      { applicationId: 'app-1', name: '轮换凭证' },
    );

    expect(keys[0]).toMatchObject({ id: 'key-1', name: '生产服务', applicationId: 'app-1' });
    expect(created.key.name).toBe('轮换凭证');
    expect(created.plaintext).toMatch(/^vsk_live_def\..+/);
  });

  it('将审核记录与总览 DTO 映射为只读结果', () => {
    const record = mapModerationRecordResponse({
      id: 'record-1',
      applicationId: 'app-1',
      applicationName: '星河电商社区',
      contentPreview: '需要关注的内容',
      decision: 'review',
      reviewRequired: true,
      riskScore: 0.76,
      reasonCodes: ['policy_required'],
      categories: [{ code: 'safety', riskScore: 0.76 }],
      route: 'ai',
      submittedAt: '2026-09-01T01:00:00Z',
    });
    const overview = mapOverviewResponse({
      todayRequests: 12,
      rejectRate: 0.08,
      recentRecords: [record],
      trend: [{ label: '09:00', total: 12, reject: 1, review: 2 }],
      decisionRail: [{ label: '规则筛查', value: '完成', tone: 'teal', detail: '已处理' }],
    });

    expect(record).toMatchObject({
      status: 'review',
      confidence: 0.76,
      category: 'safety',
      detectLevel: null,
      latencyMs: null,
      policyVersion: null,
    });
    expect(overview.todayRequests).toBe(12);
    expect(overview.requestDelta).toBeNull();
    expect(overview.recentRecords[0].status).toBe('review');
    expect(overview.trend[0]).toEqual({ label: '09:00', total: 12, reject: 1, review: 2 });
  });

  it('列表缺少分页和统计字段时返回空值，不生成当前时间', () => {
    const applications = mapApplicationListResponse({ items: [] });
    const overview = mapOverviewResponse({});
    expect(applications).toEqual({ items: [], total: 0, page: 1, pageSize: 0 });
    expect(overview).toMatchObject({
      todayRequests: null,
      rejectRate: null,
      reviewRate: null,
      p95LatencyMs: null,
      recentRecords: [],
    });
  });
});
