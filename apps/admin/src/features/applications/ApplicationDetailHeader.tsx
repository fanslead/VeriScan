import { Button, Typography } from '@douyinfe/semi-ui';
import { IconArrowLeft, IconPause, IconPlay } from '@douyinfe/semi-icons';
import type { Application } from '@/shared/api/types';
import { StatusBadge } from '@/shared/ui/StatusBadge';

interface ApplicationDetailHeaderProps {
  application: Application;
  loading: boolean;
  onBack: () => void;
  onToggle: () => void;
}

export function ApplicationDetailHeader({
  application,
  loading,
  onBack,
  onToggle,
}: ApplicationDetailHeaderProps) {
  return (
    <>
      <button className="back-link" type="button" onClick={onBack}>
        <IconArrowLeft />
        返回应用
      </button>
      <div className="detail-hero">
        <div className="detail-hero__identity">
          <span className="detail-avatar">{application.name.slice(0, 1)}</span>
          <div>
            <div className="eyebrow">应用详情 · {application.slug}</div>
            <Typography.Title heading={1}>{application.name}</Typography.Title>
            <Typography.Text type="tertiary">
              {application.description || '暂无应用说明'} ·{' '}
              {application.environment === 'live'
                ? '正式环境'
                : application.environment === 'test'
                  ? '测试环境'
                  : '暂无环境'}
            </Typography.Text>
          </div>
        </div>
        <div className="detail-hero__actions">
          <StatusBadge status={application.status} />
          <Button
            icon={application.status === 'active' ? <IconPause /> : <IconPlay />}
            loading={loading}
            onClick={onToggle}
          >
            {application.status === 'active' ? '暂停应用' : '启用应用'}
          </Button>
        </div>
      </div>
    </>
  );
}
