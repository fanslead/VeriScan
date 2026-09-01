import { Skeleton } from '@douyinfe/semi-ui';

interface LoadingBlockProps {
  rows?: number;
}

export function LoadingBlock({ rows = 4 }: LoadingBlockProps) {
  return (
    <div className="loading-block" role="status" aria-label="正在加载">
      {Array.from({ length: rows }, (_, index) => (
        <Skeleton key={index} placeholder={<Skeleton.Paragraph rows={1} />} active />
      ))}
    </div>
  );
}
