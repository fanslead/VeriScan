import { Button, Card, Tag } from '@douyinfe/semi-ui';
import type { AiConfiguration } from '@/shared/api/types';
import {
  aiTestStateMeta,
  authSchemeLabel,
  findProtocol,
  formatDateTime,
  getAiTestState,
  statusMeta,
} from './aiConfigurationLabels';

interface AiConfigurationCardProps {
  configuration: AiConfiguration;
  busy?: boolean;
  onEdit: (configuration: AiConfiguration) => void;
  onTest: (configuration: AiConfiguration) => void;
  onCreateRevision: (configuration: AiConfiguration) => void;
  onPublish: (configuration: AiConfiguration) => void;
  onActivate: (configuration: AiConfiguration) => void;
  onArchive: (configuration: AiConfiguration) => void;
}

export function AiConfigurationCard({
  configuration,
  busy = false,
  onEdit,
  onTest,
  onCreateRevision,
  onPublish,
  onActivate,
  onArchive,
}: AiConfigurationCardProps) {
  const protocol = findProtocol(configuration.protocol);
  const status = statusMeta[configuration.status];
  const testState = getAiTestState(configuration);
  const testMeta = aiTestStateMeta[testState];
  const canPublish = configuration.status === 'draft' && testState === 'passed';
  const hasTrace =
    configuration.status !== 'draft' &&
    Boolean(
      configuration.adapterContractVersion ||
        configuration.canonicalSchemaVersion ||
        configuration.canonicalSchemaHash ||
        configuration.effectiveSchemaHash ||
        configuration.schemaTransformerVersion,
    );
  const shortHash = (value: string | null) => (value ? `${value.slice(0, 8)}…` : '—');

  return (
    <Card className={`panel ai-config-card${configuration.isActive ? ' is-active' : ''}`}>
      <div className="ai-config-card__head">
        <div className="ai-config-card__identity">
          <span className="ai-config-card__mark" aria-hidden="true">
            {configuration.isActive ? '●' : '◌'}
          </span>
          <div>
            <div className="ai-config-card__title-row">
              <h2>{configuration.name}</h2>
              {configuration.isActive ? <Tag color="green">当前生效</Tag> : null}
            </div>
            <span className="ai-config-card__revision">
              {configuration.publicRevisionId || '未生成版本号'} · 更新于{' '}
              {formatDateTime(configuration.updatedAt)}
            </span>
          </div>
        </div>
        <Tag color={status.color} className="ai-config-status">
          <span className="status-dot" aria-hidden="true" />
          {status.label}
        </Tag>
      </div>

      <div className="ai-config-card__route">
        <div>
          <span className="ai-config-card__label">接口协议</span>
          <strong>{protocol.shortLabel}</strong>
          <small>{protocol.hint}</small>
        </div>
        <div>
          <span className="ai-config-card__label">模型</span>
          <strong className="ai-config-card__mono">{configuration.model || '未填写模型'}</strong>
          <small>
            {configuration.baseUrl}
            {configuration.endpointPath}
          </small>
        </div>
        <div>
          <span className="ai-config-card__label">访问凭据</span>
          <strong className="ai-config-card__mono">
            {configuration.credentialRef || '未填写引用'}
          </strong>
          <small>{authSchemeLabel(configuration.authScheme)} · 服务端安全注入</small>
        </div>
      </div>

      <div className="ai-config-card__meta">
        <span>
          数据区域 <strong>{configuration.dataRegion || '未设置'}</strong>
        </span>
        <span>
          保留策略 <strong>{configuration.retentionClass || '未设置'}</strong>
        </span>
        <span>
          请求超时 <strong>{configuration.requestTimeoutMs} ms</strong>
        </span>
        <span className={`ai-config-card__test ai-config-card__test--${testMeta.tone}`}>
          <span className="ai-config-card__test-dot" aria-hidden="true" />
          <strong>{testMeta.label}</strong>
          {configuration.lastTestedAt && testState === 'passed' ? (
            <small>{formatDateTime(configuration.lastTestedAt)}</small>
          ) : null}
        </span>
      </div>

      {hasTrace ? (
        <details className="ai-config-trace">
          <summary>查看发布追溯</summary>
          <div className="ai-config-trace__grid">
            <span>
              <small>适配器契约</small>
              <strong>{configuration.adapterContractVersion ?? '—'}</strong>
            </span>
            <span>
              <small>规范版本</small>
              <strong>{configuration.canonicalSchemaVersion ?? '—'}</strong>
            </span>
            <span>
              <small>转换器</small>
              <strong>{configuration.schemaTransformerVersion ?? '—'}</strong>
            </span>
            <span>
              <small>规范 Hash</small>
              <strong>{shortHash(configuration.canonicalSchemaHash)}</strong>
            </span>
            <span>
              <small>生效 Hash</small>
              <strong>{shortHash(configuration.effectiveSchemaHash)}</strong>
            </span>
          </div>
        </details>
      ) : null}

      <div className="ai-config-card__actions" aria-label={`${configuration.name} 操作`}>
        {configuration.status === 'draft' ? (
          <Button
            theme="borderless"
            type="tertiary"
            onClick={() => onEdit(configuration)}
            disabled={busy}
          >
            编辑草稿
          </Button>
        ) : null}
        {configuration.status !== 'draft' ? (
          <Button
            theme="borderless"
            type="tertiary"
            onClick={() => onCreateRevision(configuration)}
            disabled={busy}
          >
            创建新版本
          </Button>
        ) : null}
        {configuration.status !== 'archived' ? (
          <Button
            theme="borderless"
            type="tertiary"
            onClick={() => onTest(configuration)}
            disabled={busy}
          >
            合成测试
          </Button>
        ) : null}
        {configuration.status === 'draft' ? (
          <Button
            theme="solid"
            type="primary"
            onClick={() => onPublish(configuration)}
            loading={busy}
            disabled={!canPublish}
            title={canPublish ? '发布当前草稿' : '请先通过当前草稿的合成测试'}
          >
            发布版本
          </Button>
        ) : null}
        {configuration.status === 'published' && !configuration.isActive ? (
          <Button
            theme="solid"
            type="primary"
            onClick={() => onActivate(configuration)}
            loading={busy}
          >
            激活路由
          </Button>
        ) : null}
        {configuration.status !== 'archived' ? (
          <Button
            type="danger"
            theme="borderless"
            onClick={() => onArchive(configuration)}
            disabled={busy}
          >
            归档
          </Button>
        ) : null}
      </div>
    </Card>
  );
}
