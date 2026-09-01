import { Tag } from '@douyinfe/semi-ui';
import type { ApiKeyStatus, ApplicationStatus, ModerationStatus } from '@/shared/api/types';

interface StatusBadgeProps {
  status: ApplicationStatus | ApiKeyStatus | ModerationStatus;
  compact?: boolean;
}

const labels: Record<
  StatusBadgeProps['status'],
  { text: string; color: 'green' | 'red' | 'amber' | 'grey' }
> = {
  active: { text: '运行中', color: 'green' },
  paused: { text: '已暂停', color: 'grey' },
  revoked: { text: '已撤销', color: 'red' },
  expired: { text: '已过期', color: 'grey' },
  pass: { text: '通过', color: 'green' },
  reject: { text: '不通过', color: 'red' },
  review: { text: '建议复核', color: 'amber' },
};

export function StatusBadge({ status, compact = false }: StatusBadgeProps) {
  const item = labels[status];
  return (
    <Tag color={item.color} size={compact ? 'small' : 'default'} className="status-badge">
      <span className="status-dot" aria-hidden="true" />
      {item.text}
    </Tag>
  );
}
