import { Button, Card, Skeleton } from '@douyinfe/semi-ui';
import { IconArrowRight } from '@douyinfe/semi-icons';
import { Link } from 'react-router-dom';
import type { ApplicationUsage } from '@/shared/api/types';
import { formatDate } from '@/shared/ui/formatDate';

export function ApplicationUsageCard({
  applicationId,
  usage,
  loading,
  error,
  onRetry,
}: {
  applicationId: string;
  usage?: ApplicationUsage;
  loading: boolean;
  error: boolean;
  onRetry: () => void;
}) {
  const total = usage?.itemCount ?? 0;
  const rate = (count: number) => (total > 0 ? (count * 100) / total : 0);
  const passRate = usage ? rate(usage.passCount) : 0;
  const rejectRate = usage ? rate(usage.rejectCount) : 0;
  const reviewRate = usage ? rate(usage.reviewCount) : 0;
  const tokenValue = (value: number | null | undefined) =>
    value === null || value === undefined ? '未上报' : value.toLocaleString();

  return (
    <Card
      className="panel detail-panel"
      title={
        <div className="panel-heading">
          <div>
            <span className="section-kicker">最近 7 天</span>
            <h2>调用概览</h2>
          </div>
          <Link to={`/records?applicationId=${applicationId}`} className="panel-link">
            查看记录 <IconArrowRight />
          </Link>
        </div>
      }
    >
      {loading ? (
        <Skeleton placeholder={<Skeleton.Paragraph rows={6} />} loading active />
      ) : error ? (
        <div className="usage-error" role="alert">
          <div>
            <strong>用量暂时无法读取</strong>
            <span>审核功能不受影响，可单独重试统计请求。</span>
          </div>
          <Button type="primary" theme="light" onClick={onRetry}>
            重新读取
          </Button>
        </div>
      ) : usage && total > 0 ? (
        <>
          <div className="usage-bars">
            <div className="usage-bar">
              <span>通过</span>
              <div>
                <i style={{ width: `${passRate}%` }} />
              </div>
              <strong>{passRate.toFixed(1)}%</strong>
            </div>
            <div className="usage-bar usage-bar--red">
              <span>不通过</span>
              <div>
                <i style={{ width: `${Math.max(rejectRate, 1)}%` }} />
              </div>
              <strong>{rejectRate.toFixed(1)}%</strong>
            </div>
            <div className="usage-bar usage-bar--amber">
              <span>建议复核</span>
              <div>
                <i style={{ width: `${Math.max(reviewRate, 1)}%` }} />
              </div>
              <strong>{reviewRate.toFixed(1)}%</strong>
            </div>
          </div>
          <div className="usage-facts">
            <div>
              <span>请求 / 内容</span>
              <strong>
                {usage.requestCount.toLocaleString()} / {usage.itemCount.toLocaleString()}
              </strong>
            </div>
            <div>
              <span>AI 调用</span>
              <strong>{usage.aiCallCount.toLocaleString()}</strong>
            </div>
            <div>
              <span>输入 / 输出 Token</span>
              <strong>
                {tokenValue(usage.aiInputTokens)} / {tokenValue(usage.aiOutputTokens)}
              </strong>
            </div>
            <div>
              <span>AI 失败</span>
              <strong className={usage.aiFailureCount > 0 ? 'usage-fact--danger' : undefined}>
                {usage.aiFailureCount.toLocaleString()}
              </strong>
            </div>
          </div>
          <div className="usage-footnote">
            数据窗口：
            {formatDate(usage.dataFrom, { month: '2-digit', day: '2-digit', hour: '2-digit' })}
            {' — '}
            {formatDate(usage.dataThrough, { month: '2-digit', day: '2-digit', hour: '2-digit' })}
          </div>
        </>
      ) : (
        <div className="mini-empty">当前时间窗内暂无审核调用</div>
      )}
    </Card>
  );
}
