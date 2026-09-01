import { Card, Empty, Skeleton } from '@douyinfe/semi-ui';
import type { AiConfiguration } from '@/shared/api/types';
import { AiConfigurationCard } from './AiConfigurationCard';

interface AiConfigurationListProps {
  configurations: AiConfiguration[];
  loading: boolean;
  error: boolean;
  busyId: string | null;
  onRetry: () => void;
  onEdit: (configuration: AiConfiguration) => void;
  onTest: (configuration: AiConfiguration) => void;
  onCreateRevision: (configuration: AiConfiguration) => void;
  onPublish: (configuration: AiConfiguration) => void;
  onActivate: (configuration: AiConfiguration) => void;
  onArchive: (configuration: AiConfiguration) => void;
}

export function AiConfigurationList({
  configurations,
  loading,
  error,
  busyId,
  onRetry,
  onEdit,
  onTest,
  onCreateRevision,
  onPublish,
  onActivate,
  onArchive,
}: AiConfigurationListProps) {
  if (loading) {
    return (
      <Card className="panel ai-config-list-panel">
        <div className="ai-config-list__skeleton" aria-label="正在加载 AI 配置">
          <Skeleton.Paragraph rows={5} />
          <Skeleton.Paragraph rows={5} />
        </div>
      </Card>
    );
  }

  if (error) {
    return (
      <Card className="panel ai-config-list-panel">
        <div className="ai-config-list__error" role="alert">
          <strong>AI 配置暂时无法加载</strong>
          <span>请检查连接后重试，已有配置不会受到影响。</span>
          <button type="button" onClick={onRetry}>
            重新加载
          </button>
        </div>
      </Card>
    );
  }

  if (configurations.length === 0) {
    return (
      <Card className="panel ai-config-list-panel">
        <div className="ai-config-empty">
          <Empty
            title="还没有 AI 配置"
            description="创建第一份草稿，连接外部模型后再发布到线上。"
          />
        </div>
      </Card>
    );
  }

  return (
    <div className="ai-config-list" aria-label="AI 配置列表">
      {configurations.map((configuration) => (
        <AiConfigurationCard
          key={configuration.id}
          configuration={configuration}
          busy={busyId === configuration.id}
          onEdit={onEdit}
          onTest={onTest}
          onCreateRevision={onCreateRevision}
          onPublish={onPublish}
          onActivate={onActivate}
          onArchive={onArchive}
        />
      ))}
    </div>
  );
}
