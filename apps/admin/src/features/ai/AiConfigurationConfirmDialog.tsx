import { Button, Modal, Typography } from '@douyinfe/semi-ui';
import type { AiConfiguration } from '@/shared/api/types';

export type AiConfigurationAction = 'publish' | 'activate' | 'archive';

interface AiConfigurationConfirmDialogProps {
  visible: boolean;
  configuration: AiConfiguration | null;
  action: AiConfigurationAction | null;
  loading?: boolean;
  onCancel: () => void;
  onConfirm: () => void;
}

const copy: Record<AiConfigurationAction, { title: string; description: string; confirm: string }> =
  {
    publish: {
      title: '发布这份 AI 配置？',
      description: '发布后配置内容会冻结，只有重新创建草稿才能调整。发布不会自动切换当前线上路由。',
      confirm: '确认发布',
    },
    activate: {
      title: '激活这份 AI 配置？',
      description:
        '激活后会切换线上 AI 路由，当前生效版本会停止接收新请求。这个动作也可用于回滚到已发布版本。',
      confirm: '确认激活',
    },
    archive: {
      title: '归档这份 AI 配置？',
      description:
        '归档后不能再测试、发布或激活，只保留版本和审核记录。若它当前生效，线上路由也会停止使用。',
      confirm: '确认归档',
    },
  };

export function AiConfigurationConfirmDialog({
  visible,
  configuration,
  action,
  loading = false,
  onCancel,
  onConfirm,
}: AiConfigurationConfirmDialogProps) {
  if (!action) return null;
  const wording = copy[action];
  const isDanger = action === 'archive';

  return (
    <Modal
      visible={visible}
      title={wording.title}
      onCancel={onCancel}
      footer={[
        <Button key="cancel" onClick={onCancel} disabled={loading}>
          先不操作
        </Button>,
        <Button
          key="confirm"
          type={isDanger ? 'danger' : 'primary'}
          theme="solid"
          loading={loading}
          onClick={onConfirm}
        >
          {wording.confirm}
        </Button>,
      ]}
    >
      <div className={`ai-config-confirm ai-config-confirm--${action}`}>
        <span className="ai-config-confirm__mark" aria-hidden="true">
          {isDanger ? '!' : '→'}
        </span>
        <div>
          <strong>{configuration?.name ?? '这份配置'}</strong>
          <Typography.Text>{wording.description}</Typography.Text>
        </div>
      </div>
    </Modal>
  );
}
