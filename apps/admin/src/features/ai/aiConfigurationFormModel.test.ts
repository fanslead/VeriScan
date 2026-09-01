import { describe, expect, it } from 'vitest';
import type { AiConfigurationDraftInput } from '@/shared/api/types';
import { createInitialValues, validateAiConfiguration } from './aiConfigurationFormModel';
import { getAiTestState } from './aiConfigurationLabels';

const validDraft = (): AiConfigurationDraftInput => ({
  ...createInitialValues(),
  name: '主路由',
  model: 'gpt-4o-mini',
  apiKey: 'sk-test-key',
});

describe('AI 配置表单规则', () => {
  it('OpenAI 兼容协议无版本或 Query 版本均可，Header 被拒绝', () => {
    const withoutVersion = validateAiConfiguration(validDraft());
    expect(withoutVersion.apiVersion).toBeUndefined();
    expect(withoutVersion.apiVersionLocation).toBeUndefined();

    const withQuery = validateAiConfiguration({
      ...validDraft(),
      apiVersion: '2024-10-21',
      apiVersionLocation: 'query',
    });
    expect(withQuery.apiVersionLocation).toBeUndefined();

    const withHeader = validateAiConfiguration({
      ...validDraft(),
      apiVersion: '2024-10-21',
      apiVersionLocation: 'header',
    });
    expect(withHeader.apiVersionLocation).toContain('只能通过固定 Query');
  });

  it('Messages 协议必须显式配置版本并固定 Header', () => {
    const missing = validateAiConfiguration({
      ...validDraft(),
      protocol: 'anthropicMessages',
      authScheme: 'xApiKey',
      endpointPath: '/v1/messages',
      apiVersion: null,
      apiVersionLocation: 'header',
    });
    expect(missing.apiVersion).toContain('必须填写');

    const validInput: AiConfigurationDraftInput = {
      ...validDraft(),
      protocol: 'anthropicMessages',
      authScheme: 'xApiKey',
      endpointPath: '/v1/messages',
      apiVersion: '2023-06-01',
      apiVersionLocation: 'header',
    };
    const valid = validateAiConfiguration(validInput);
    expect(valid.apiVersion).toBeUndefined();
    expect(valid.apiVersionLocation).toBeUndefined();

    const query = validateAiConfiguration({
      ...validInput,
      apiVersion: '2023-06-01',
      apiVersionLocation: 'query',
    });
    expect(query.apiVersionLocation).toContain('只允许');
  });

  it('外部模型地址必须使用 HTTPS 且不包含路径', () => {
    expect(
      validateAiConfiguration({ ...validDraft(), baseUrl: 'http://api.example.com' }).baseUrl,
    ).toContain('HTTPS');
    expect(
      validateAiConfiguration({ ...validDraft(), baseUrl: 'https://api.example.com/v1' }).baseUrl,
    ).toContain('HTTPS');
    expect(
      validateAiConfiguration({ ...validDraft(), baseUrl: 'https://api.example.com' }).baseUrl,
    ).toBeUndefined();
  });

  it('已发布版本的测试结果不因发布动作更新的 updatedAt 被误标过期', () => {
    const configuration = {
      ...validDraft(),
      id: 'ai-config-1',
      publicRevisionId: 'ai-model@1',
      credentialRef: 'managed://encrypted',
      hasCredential: true,
      credentialSource: 'managed' as const,
      status: 'published' as const,
      isActive: true,
      createdAt: '2026-09-01T01:00:00Z',
      updatedAt: '2026-09-01T02:00:00Z',
      publishedAt: '2026-09-01T02:00:00Z',
      lastTestedAt: '2026-09-01T01:30:00Z',
      lastTestSucceeded: true,
      lastTestFailureCode: null,
      adapterContractVersion: '2026.09',
      canonicalSchemaVersion: 'moderation.v1',
      canonicalSchemaHash: 'hash',
      effectiveSchemaHash: 'hash',
      schemaTransformerVersion: 'transformer-2',
    };
    expect(getAiTestState(configuration)).toBe('passed');
    expect(
      getAiTestState({ ...configuration, status: 'draft', lastTestedAt: '2026-09-01T01:30:00Z' }),
    ).toBe('stale');
  });
});
