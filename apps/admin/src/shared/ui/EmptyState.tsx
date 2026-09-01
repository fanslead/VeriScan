import { Button, Empty } from '@douyinfe/semi-ui';
import { IllustrationNoResult } from '@douyinfe/semi-illustrations';

interface EmptyStateProps {
  title: string;
  description: string;
  actionText?: string;
  onAction?: () => void;
}

export function EmptyState({ title, description, actionText, onAction }: EmptyStateProps) {
  return (
    <div className="empty-state">
      <Empty
        image={<IllustrationNoResult style={{ width: 140, height: 100 }} />}
        title={title}
        description={description}
      />
      {actionText && onAction ? (
        <Button theme="solid" type="primary" onClick={onAction}>
          {actionText}
        </Button>
      ) : null}
    </div>
  );
}
