import { IconArrowRight } from '@douyinfe/semi-icons';
import { Link } from 'react-router-dom';
import type { ModerationRecord } from '@/shared/api/types';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { formatDate } from '@/shared/ui/formatDate';

export function RecentRecord({ record }: { record: ModerationRecord }) {
  return (
    <Link to={`/records/${record.id}`} className="recent-record">
      <div className="recent-record__status">
        <StatusBadge status={record.status} compact />
      </div>
      <div className="recent-record__body">
        <strong>{record.contentPreview}</strong>
        <span>
          {record.applicationName} ·{' '}
          {formatDate(record.createdAt, {
            hour: '2-digit',
            minute: '2-digit',
          })}
        </span>
      </div>
      <IconArrowRight className="recent-record__arrow" />
    </Link>
  );
}
