import { Card, Tag } from '@douyinfe/semi-ui';
import type { AiConfiguration } from '@/shared/api/types';
import { findProtocol } from './aiConfigurationLabels';

export function AiConfigurationSummary({ configurations }: { configurations: AiConfiguration[] }) {
  const active = configurations.find((configuration) => configuration.isActive);
  const drafts = configurations.filter((configuration) => configuration.status === 'draft').length;
  const published = configurations.filter(
    (configuration) => configuration.status === 'published',
  ).length;

  return (
    <section className="ai-config-summary" aria-label="AI 路由摘要">
      <Card className="panel ai-config-summary__active">
        <div className="ai-config-summary__eyebrow">当前生效模型</div>
        <div className="ai-config-summary__active-row">
          <span
            className={`ai-config-summary__pulse${active ? '' : ' is-empty'}`}
            aria-hidden="true"
          />
          <div>
            <strong>{active?.name ?? '尚未激活模型路由'}</strong>
            <span>
              {active
                ? `${findProtocol(active.protocol).shortLabel} · ${active.model}`
                : '发布一份配置后即可切换线上判定能力'}
            </span>
          </div>
          <Tag color={active ? 'green' : 'grey'}>{active ? '运行中' : '未配置'}</Tag>
        </div>
      </Card>
      <div className="ai-config-summary__stats">
        <div>
          <strong>{configurations.length}</strong>
          <span>配置总数</span>
        </div>
        <div>
          <strong>{drafts}</strong>
          <span>待发布草稿</span>
        </div>
        <div>
          <strong>{published}</strong>
          <span>已发布版本</span>
        </div>
      </div>
    </section>
  );
}
