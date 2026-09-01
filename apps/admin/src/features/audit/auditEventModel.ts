import type { AuditEvent } from '@/shared/api/types';

const actionLabels: Record<string, string> = {
  'application.created': '创建应用',
  'application.updated': '更新应用',
  'application.rule_set_bound': '切换应用规则',
  'api_key.created': '创建接入密钥',
  'api_key.rotated': '轮换接入密钥',
  'api_key.revoked': '撤销接入密钥',
  'api_key.revoked_for_rotation': '轮换时撤销旧密钥',
  'rule_set.created': '创建规则草稿',
  'rule_set.updated': '更新规则草稿',
  'rule_set.published': '发布规则版本',
  'rule_set.archived': '归档规则版本',
  'ai_configuration.created': '创建 AI 配置',
  'ai_configuration.updated': '更新 AI 配置',
  'ai_configuration.published': '发布 AI 配置',
  'ai_configuration.activated': '切换 AI 路由',
  'ai_configuration.archived': '归档 AI 配置',
};

const resourceLabels: Record<string, string> = {
  application: '应用',
  api_key: 'API Key',
  rule_set: '规则版本',
  ai_configuration: 'AI 配置',
  moderation_request: '审核请求',
};

const fieldLabels: Record<string, string> = {
  status: '状态',
  environment: '运行环境',
  revision: '版本',
  ruleSetVersionId: '规则版本',
  protocol: '连接方式',
  model: '模型',
  isActive: '是否生效',
  keyPrefix: '密钥标识',
  expiresAt: '到期时间',
  ruleCount: '规则数量',
  normalizationProfile: '文本处理方式',
};

const readPayload = (value: string | null): Record<string, unknown> => {
  if (!value) return {};
  try {
    const parsed = JSON.parse(value) as unknown;
    return parsed && typeof parsed === 'object' && !Array.isArray(parsed)
      ? (parsed as Record<string, unknown>)
      : {};
  } catch {
    return {};
  }
};

const formatValue = (value: unknown): string => {
  if (value === null || value === undefined || value === '') return '未设置';
  if (typeof value === 'boolean') return value ? '是' : '否';
  if (typeof value === 'string') {
    const statusLabels: Record<string, string> = {
      Active: '运行中',
      Suspended: '已暂停',
      Archived: '已归档',
      Draft: '草稿',
      Published: '已发布',
      Revoked: '已撤销',
    };
    return statusLabels[value] ?? value;
  }
  return String(value);
};

export interface AuditChange {
  label: string;
  before: string;
  after: string;
}

export const getAuditActionLabel = (action: string) => actionLabels[action] ?? '配置发生变更';

export const getAuditResourceLabel = (resourceType: string) =>
  resourceLabels[resourceType] ?? '系统配置';

export const getAuditChanges = (event: AuditEvent): AuditChange[] => {
  const before = readPayload(event.beforeJson);
  const after = readPayload(event.afterJson);
  const keys = new Set([...Object.keys(before), ...Object.keys(after)]);
  const ignored = new Set([
    'action',
    'applicationId',
    'apiKeyId',
    'configurationId',
    'ruleSetVersionId',
    'publicId',
    'publicKeyId',
    'checksum',
  ]);

  return [...keys]
    .filter((key) => !ignored.has(key))
    .filter((key) => JSON.stringify(before[key]) !== JSON.stringify(after[key]))
    .map((key) => ({
      label: fieldLabels[key] ?? key,
      before: formatValue(before[key]),
      after: formatValue(after[key]),
    }));
};

export const auditActionOptions = Object.entries(actionLabels).map(([value, label]) => ({
  value,
  label,
}));
