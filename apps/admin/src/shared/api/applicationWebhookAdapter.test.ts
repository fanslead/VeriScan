import { describe, expect, it } from 'vitest';
import {
  mapApplicationWebhookResponse,
  mapApplicationWebhookSavedResponse,
  mapApplicationWebhookSecretResponse,
  mapApplicationWebhookTestAcceptedResponse,
  mapApplicationWebhookTestResponse,
} from './applicationWebhookAdapter';

const applicationId = '6f2e7d2c-95a7-4f4c-8cb6-0d73a6f99a33';

const webhookResponse = {
  configured: true,
  id: 'f2c56b7c-9a6f-4f3a-8a1e-4ddc8e7b7e5c',
  applicationId,
  endpointUrl: 'https://example.com/veriscan/webhook',
  enabled: false,
  revision: 2,
  currentRevisionTested: false,
  lastTestId: 'e51c0d0f-12ab-4ec4-9f42-3bbd4263a3e2',
  lastTestStatus: 'failed',
  lastTestHttpStatusCode: 500,
  lastTestLatencyMilliseconds: 120,
  lastTestedAt: '2026-09-02T04:00:00Z',
  updatedAt: '2026-09-02T04:00:01Z',
};

describe('application webhook adapter', () => {
  it('映射配置、保留明确的空值并不生成连接测试结果', () => {
    expect(mapApplicationWebhookResponse(webhookResponse)).toEqual(webhookResponse);
    expect(
      mapApplicationWebhookResponse({
        configured: false,
        id: null,
        applicationId,
        endpointUrl: null,
        enabled: false,
        revision: null,
        currentRevisionTested: false,
        lastTestId: null,
        lastTestStatus: null,
        lastTestHttpStatusCode: null,
        lastTestLatencyMilliseconds: null,
        lastTestedAt: null,
        updatedAt: null,
      }),
    ).toMatchObject({ configured: false, enabled: false, revision: null });
  });

  it('解析保存、测试提交和轮换密钥响应', () => {
    expect(
      mapApplicationWebhookSavedResponse({
        webhook: webhookResponse,
        signingSecret: 'whsec_initial_secret',
      }),
    ).toMatchObject({ signingSecret: 'whsec_initial_secret', webhook: webhookResponse });
    expect(
      mapApplicationWebhookTestAcceptedResponse({
        testId: 'test-1',
        statusUrl: `/api/admin/v1/applications/${applicationId}/webhook/tests/test-1`,
        submittedAt: '2026-09-02T04:01:00Z',
      }),
    ).toEqual({
      testId: 'test-1',
      statusUrl: `/api/admin/v1/applications/${applicationId}/webhook/tests/test-1`,
      submittedAt: '2026-09-02T04:01:00Z',
    });
    expect(
      mapApplicationWebhookSecretResponse({
        signingSecret: 'whsec_rotated_secret',
        rotatedAt: '2026-09-02T04:02:00Z',
      }),
    ).toEqual({ signingSecret: 'whsec_rotated_secret', rotatedAt: '2026-09-02T04:02:00Z' });
  });

  it('解析已完成测试，并拒绝缺少必需字段或伪造成功状态的响应', () => {
    expect(
      mapApplicationWebhookTestResponse({
        testId: 'test-1',
        applicationId,
        configurationRevision: 2,
        status: 'succeeded',
        httpStatusCode: 200,
        latencyMilliseconds: 31,
        failureCode: null,
        submittedAt: '2026-09-02T04:01:00Z',
        completedAt: '2026-09-02T04:01:01Z',
      }),
    ).toMatchObject({ status: 'succeeded', httpStatusCode: 200 });

    expect(() =>
      mapApplicationWebhookResponse({
        configured: true,
        applicationId,
        endpointUrl: 'https://example.com/webhook',
        enabled: true,
        revision: 1,
        currentRevisionTested: true,
      }),
    ).toThrow('响应数据无效');
    expect(() =>
      mapApplicationWebhookTestResponse({
        testId: 'test-1',
        applicationId,
        configurationRevision: 2,
        status: 'succeeded',
        httpStatusCode: null,
        latencyMilliseconds: null,
        failureCode: null,
        submittedAt: '2026-09-02T04:01:00Z',
        completedAt: null,
      }),
    ).toThrow('响应数据无效');
    expect(() =>
      mapApplicationWebhookResponse({
        ...webhookResponse,
        revision: 1.5,
      }),
    ).toThrow('响应数据无效');
  });
});
