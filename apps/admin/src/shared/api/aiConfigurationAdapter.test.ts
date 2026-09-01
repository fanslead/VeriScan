import { describe, expect, it } from 'vitest';
import {
  mapAiConfigurationListResponse,
  mapAiConfigurationResponse,
  mapAiConfigurationTestResponse,
} from './aiConfigurationAdapter';

const response = {
  id: 'f93b7e8f-1f2a-47bd-8f77-b5e4e99d9b18',
  publicRevisionId: 'ai-model@f93b7e8f',
  name: '主路由',
  protocol: 'anthropicMessages',
  baseUrl: 'https://api.anthropic.com',
  endpointPath: '/v1/messages',
  credentialRef: 'config://anthropic-prod',
  hasCredential: true,
  credentialSource: 'managed',
  authScheme: 'xApiKey',
  model: 'claude-3-5-haiku-latest',
  apiVersion: '2023-06-01',
  apiVersionLocation: 'header',
  systemPrompt: '这是一个用于测试的安全审核系统提示词，长度超过二十个字符。',
  decodingMode: 'providerFixed',
  maxInputTokens: 4096,
  maxOutputTokens: 512,
  connectTimeoutMs: 2000,
  requestTimeoutMs: 15000,
  maxAttempts: 2,
  dataRegion: 'global',
  retentionClass: '30d',
  status: 'published',
  isActive: true,
  createdAt: '2026-09-01T01:00:00Z',
  updatedAt: '2026-09-01T02:00:00Z',
  publishedAt: '2026-09-01T01:30:00Z',
  lastTestedAt: '2026-09-01T01:45:00Z',
  lastTestSucceeded: true,
  lastTestFailureCode: null,
  adapterContractVersion: '2026.09',
  canonicalSchemaVersion: 'moderation.v1',
  canonicalSchemaHash: '9f88a7c312d58ccf52f2c1b4d7e9223ef17a889cc71e8902c6c52c49ea3d04ab',
  effectiveSchemaHash: 'bb29d21d3f4f08007f94af306e50eabf1b7fd9bf3df08b7fa1da4d659d30d0b1',
  schemaTransformerVersion: 'transformer-2',
};

describe('AI 配置 DTO 适配', () => {
  it('保留三种协议、版本发送位置和测试门禁字段', () => {
    const configuration = mapAiConfigurationResponse(response);
    expect(configuration).toMatchObject({
      id: response.id,
      protocol: 'anthropicMessages',
      apiVersion: '2023-06-01',
      apiVersionLocation: 'header',
      lastTestSucceeded: true,
      lastTestFailureCode: null,
      adapterContractVersion: '2026.09',
      canonicalSchemaVersion: 'moderation.v1',
      canonicalSchemaHash: response.canonicalSchemaHash,
      effectiveSchemaHash: response.effectiveSchemaHash,
      schemaTransformerVersion: 'transformer-2',
      status: 'published',
      isActive: true,
      hasCredential: true,
      credentialSource: 'managed',
    });
  });

  it('列表缺少 items 时返回空列表，不伪造配置', () => {
    expect(mapAiConfigurationListResponse({})).toEqual([]);
    expect(mapAiConfigurationListResponse({ items: [response] })).toHaveLength(1);
  });

  it('未知枚举值使用安全的不可激活默认值', () => {
    expect(
      mapAiConfigurationResponse({
        ...response,
        protocol: 'unknown',
        authScheme: 'unknown',
        apiVersionLocation: 'unknown',
        status: 'unknown',
      }),
    ).toMatchObject({
      protocol: 'openAiChatCompletions',
      authScheme: 'bearer',
      apiVersionLocation: 'none',
      status: 'draft',
      isActive: false,
    });
  });

  it('解析合成测试响应并保留缺失 token 的未知状态', () => {
    expect(
      mapAiConfigurationTestResponse({
        succeeded: false,
        protocol: 'openAiResponses',
        model: 'gpt-4o-mini',
        latencyMs: 480,
        failureCode: 'invalid_output',
      }),
    ).toEqual({
      succeeded: false,
      protocol: 'openAiResponses',
      model: 'gpt-4o-mini',
      latencyMs: 480,
      inputTokens: null,
      outputTokens: null,
      failureCode: 'invalid_output',
    });
  });
});
