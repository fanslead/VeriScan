import type {
  AiApiVersionLocation,
  AiAuthScheme,
  AiConfiguration,
  AiConfigurationStatus,
  AiDecodingMode,
  AiProtocol,
} from '@/shared/api/types';

export const protocolOptions: Array<{
  value: AiProtocol;
  label: string;
  shortLabel: string;
  hint: string;
  baseUrl: string;
  endpointPath: string;
  authScheme: AiAuthScheme;
  apiVersion: string | null;
  apiVersionLocation: AiApiVersionLocation;
}> = [
  {
    value: 'openAiChatCompletions',
    label: 'Chat Completions · OpenAI 兼容',
    shortLabel: 'Chat Completions',
    hint: '适用于大多数 OpenAI 兼容网关和模型服务。',
    baseUrl: 'https://api.openai.com',
    endpointPath: '/v1/chat/completions',
    authScheme: 'bearer',
    apiVersion: null,
    apiVersionLocation: 'none',
  },
  {
    value: 'openAiResponses',
    label: 'Responses API · OpenAI 兼容',
    shortLabel: 'Responses API',
    hint: '适用于支持 Responses API 输入输出结构的服务。',
    baseUrl: 'https://api.openai.com',
    endpointPath: '/v1/responses',
    authScheme: 'bearer',
    apiVersion: null,
    apiVersionLocation: 'none',
  },
  {
    value: 'anthropicMessages',
    label: 'Messages API · Anthropic 兼容',
    shortLabel: 'Messages API',
    hint: '适用于 Anthropic Messages API 及其兼容网关。',
    baseUrl: 'https://api.anthropic.com',
    endpointPath: '/v1/messages',
    authScheme: 'xApiKey',
    apiVersion: '2023-06-01',
    apiVersionLocation: 'header',
  },
];

export const authSchemeOptions: Array<{ value: AiAuthScheme; label: string }> = [
  { value: 'bearer', label: 'Bearer Token' },
  { value: 'xApiKey', label: 'X-API-Key' },
  { value: 'apiKey', label: 'API Key' },
];

export const apiVersionLocationOptions: Array<{
  value: AiApiVersionLocation;
  label: string;
}> = [
  { value: 'none', label: '不发送版本参数' },
  { value: 'header', label: '受控 Header' },
  { value: 'query', label: '固定 Query 参数' },
];

export const decodingModeOptions: Array<{ value: AiDecodingMode; label: string; hint: string }> = [
  {
    value: 'omitTemperature',
    label: '不发送 temperature',
    hint: '交给服务商使用默认采样策略。',
  },
  {
    value: 'sendTemperatureZero',
    label: '固定为 0',
    hint: '仅适用于明确支持 temperature=0 的服务。',
  },
  {
    value: 'providerFixed',
    label: '服务商固定',
    hint: '不由明鉴控制解码参数。',
  },
];

export const statusMeta: Record<
  AiConfigurationStatus,
  { label: string; color: 'green' | 'amber' | 'grey'; description: string }
> = {
  draft: { label: '草稿', color: 'amber', description: '可继续编辑，尚未进入线上路由' },
  published: { label: '已发布', color: 'grey', description: '内容已冻结，可激活到线上路由' },
  archived: { label: '已归档', color: 'grey', description: '历史版本，仅保留审计记录' },
};

export const findProtocol = (protocol: AiProtocol) =>
  protocolOptions.find((item) => item.value === protocol) ?? protocolOptions[0];

export const authSchemeLabel = (scheme: AiAuthScheme) =>
  authSchemeOptions.find((item) => item.value === scheme)?.label ?? '服务商凭据';

export const formatDateTime = (value: string | null | undefined) => {
  if (!value) return '暂无时间';
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return '暂无时间';
  return new Intl.DateTimeFormat('zh-CN', {
    month: '2-digit',
    day: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
};

export type AiTestState = 'passed' | 'failed' | 'stale' | 'untested';

export const getAiTestState = (configuration: AiConfiguration): AiTestState => {
  if (!configuration.lastTestedAt || configuration.lastTestSucceeded === null) return 'untested';
  if (
    configuration.status === 'draft' &&
    Date.parse(configuration.lastTestedAt) < Date.parse(configuration.updatedAt)
  ) {
    return 'stale';
  }
  return configuration.lastTestSucceeded ? 'passed' : 'failed';
};

export const aiTestStateMeta: Record<
  AiTestState,
  { label: string; tone: 'green' | 'amber' | 'red' | 'grey' }
> = {
  passed: { label: '测试通过', tone: 'green' },
  failed: { label: '测试未通过', tone: 'red' },
  stale: { label: '配置已变更，需重测', tone: 'amber' },
  untested: { label: '尚未测试', tone: 'grey' },
};
