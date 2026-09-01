import { Button, Empty, Typography } from '@douyinfe/semi-ui';
import { IllustrationFailure } from '@douyinfe/semi-illustrations';

interface ErrorStateProps {
  title?: string;
  description?: string;
  onRetry: () => void;
}

export function ErrorState({
  title = '暂时无法加载',
  description = '请检查连接后重试，已有内容不会受到影响。',
  onRetry,
}: ErrorStateProps) {
  return (
    <div className="error-state" role="alert">
      <Empty
        image={<IllustrationFailure style={{ width: 132, height: 96 }} />}
        title=""
        description=""
      />
      <Typography.Title heading={5}>{title}</Typography.Title>
      <Typography.Text type="tertiary">{description}</Typography.Text>
      <Button onClick={onRetry}>重新加载</Button>
    </div>
  );
}
