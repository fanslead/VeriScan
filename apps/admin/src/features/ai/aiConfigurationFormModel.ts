import type { AiConfiguration, AiConfigurationDraftInput } from '@/shared/api/types';

export const defaultPrompt =
  '你是内容安全审核助手。只返回约定的 JSON 结构，并依据审核策略给出安全、违规或需复核的结论。';

export const createInitialValues = (): AiConfigurationDraftInput => ({
  name: '',
  protocol: 'openAiChatCompletions',
  baseUrl: 'https://api.openai.com',
  endpointPath: '/v1/chat/completions',
  credentialRef: 'config://',
  authScheme: 'bearer',
  model: '',
  apiVersion: null,
  apiVersionLocation: 'none',
  systemPrompt: defaultPrompt,
  decodingMode: 'omitTemperature',
  maxInputTokens: 4096,
  maxOutputTokens: 512,
  connectTimeoutMs: 2000,
  requestTimeoutMs: 15000,
  maxAttempts: 2,
  dataRegion: 'global',
  retentionClass: '30d',
});

export const toDraftValues = (configuration: AiConfiguration): AiConfigurationDraftInput => ({
  name: configuration.name,
  protocol: configuration.protocol,
  baseUrl: configuration.baseUrl,
  endpointPath: configuration.endpointPath,
  credentialRef: configuration.credentialRef,
  authScheme: configuration.authScheme,
  model: configuration.model,
  apiVersion: configuration.apiVersion,
  apiVersionLocation: configuration.apiVersionLocation,
  systemPrompt: configuration.systemPrompt,
  decodingMode: configuration.decodingMode,
  maxInputTokens: configuration.maxInputTokens,
  maxOutputTokens: configuration.maxOutputTokens,
  connectTimeoutMs: configuration.connectTimeoutMs,
  requestTimeoutMs: configuration.requestTimeoutMs,
  maxAttempts: configuration.maxAttempts,
  dataRegion: configuration.dataRegion,
  retentionClass: configuration.retentionClass,
});

export type FieldKey = keyof AiConfigurationDraftInput;
export type FieldErrors = Partial<Record<FieldKey, string>>;

export const validateAiConfiguration = (values: AiConfigurationDraftInput): FieldErrors => {
  const errors: FieldErrors = {};
  if (values.name.trim().length < 2) errors.name = '请输入至少 2 个字的配置名称';
  if (!values.model.trim()) errors.model = '请输入模型名称';
  if (!/^https:\/\/[^\s/]+(?::\d+)?$/.test(values.baseUrl.trim())) {
    errors.baseUrl = '请输入仅包含 HTTPS、主机名和可选端口的地址';
  }
  if (
    !values.endpointPath.startsWith('/') ||
    values.endpointPath.startsWith('//') ||
    values.endpointPath.includes('?') ||
    values.endpointPath.includes('#')
  ) {
    errors.endpointPath = '请输入不带查询参数的路径';
  }
  if (!/^config:\/\/[A-Za-z][A-Za-z0-9_.-]{0,127}$/.test(values.credentialRef.trim())) {
    errors.credentialRef = '请使用 config://名称 格式';
  }
  const hasApiVersion = Boolean(values.apiVersion?.trim());
  if (values.protocol === 'anthropicMessages') {
    if (!hasApiVersion) errors.apiVersion = 'Messages 协议必须填写服务商版本';
    if (values.apiVersionLocation !== 'header') {
      errors.apiVersionLocation = 'Messages 协议只允许通过受控 Header 发送版本';
    }
  } else if (!hasApiVersion && values.apiVersionLocation !== 'none') {
    errors.apiVersionLocation = '未填写版本时请选择不发送版本参数';
  } else if (hasApiVersion && values.apiVersionLocation !== 'query') {
    errors.apiVersionLocation = 'OpenAI 兼容协议的版本只能通过固定 Query 参数发送';
  }
  if (values.systemPrompt.trim().length < 20) errors.systemPrompt = '系统提示词至少需要 20 个字符';
  if (values.requestTimeoutMs <= values.connectTimeoutMs) {
    errors.requestTimeoutMs = '请求超时应大于连接超时';
  }
  if (!values.dataRegion.trim()) errors.dataRegion = '请输入数据区域';
  if (!values.retentionClass.trim()) errors.retentionClass = '请输入保留策略';
  if (values.maxInputTokens < 128 || values.maxInputTokens > 1_000_000) {
    errors.maxInputTokens = '请输入 128 到 1,000,000 之间的数值';
  }
  if (values.maxOutputTokens < 32 || values.maxOutputTokens > 32_768) {
    errors.maxOutputTokens = '请输入 32 到 32,768 之间的数值';
  }
  if (values.connectTimeoutMs < 100 || values.connectTimeoutMs > 30_000) {
    errors.connectTimeoutMs = '请输入 100 到 30,000 之间的数值';
  }
  if (values.requestTimeoutMs < 500 || values.requestTimeoutMs > 120_000) {
    errors.requestTimeoutMs = '请输入 500 到 120,000 之间的数值';
  }
  if (values.maxAttempts < 1 || values.maxAttempts > 3) {
    errors.maxAttempts = '请输入 1 到 3 之间的数值';
  }
  return errors;
};

export const canUseSuggestion = <T>(current: T, previousSuggestion: T, empty: T) =>
  current === previousSuggestion || current === empty;
