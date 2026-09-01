import { Button, Card, Skeleton, Tag } from '@douyinfe/semi-ui';
import { IconArrowRight, IconKey, IconPlus } from '@douyinfe/semi-icons';
import { Link } from 'react-router-dom';
import type { ApiKey } from '@/shared/api/types';
import { formatDate } from '@/shared/ui/formatDate';

interface ApplicationCredentialsCardProps {
  applicationId: string;
  activeKeys: ApiKey[];
  loading: boolean;
  onCreate: () => void;
}

export function ApplicationCredentialsCard({
  applicationId,
  activeKeys,
  loading,
  onCreate,
}: ApplicationCredentialsCardProps) {
  return (
    <Card
      className="panel detail-panel"
      title={
        <div className="panel-heading">
          <div>
            <span className="section-kicker">接入密钥</span>
            <h2>API Key</h2>
          </div>
          <Link to={`/applications/${applicationId}/keys`} className="panel-link">
            管理凭证 <IconArrowRight />
          </Link>
        </div>
      }
    >
      <div className="key-summary">
        <div className="key-summary__icon">
          <IconKey />
        </div>
        <div>
          <strong>{activeKeys.length} 枚有效凭证</strong>
          <span>
            上次使用{' '}
            {activeKeys[0]?.lastUsedAt
              ? formatDate(activeKeys[0].lastUsedAt, {
                  month: 'numeric',
                  day: 'numeric',
                  hour: '2-digit',
                  minute: '2-digit',
                })
              : '暂无记录'}
          </span>
        </div>
      </div>
      <div className="key-list-preview">
        {loading ? (
          <Skeleton.Paragraph rows={2} />
        ) : (
          activeKeys.slice(0, 2).map((key) => (
            <div className="key-preview-row" key={key.id}>
              <code>{key.prefix}••••••••</code>
              <Tag color="green">有效</Tag>
            </div>
          ))
        )}
        {activeKeys.length === 0 && !loading ? (
          <div className="mini-empty">还没有有效凭证</div>
        ) : null}
      </div>
      <Button block icon={<IconPlus />} onClick={onCreate}>
        创建 API Key
      </Button>
    </Card>
  );
}
