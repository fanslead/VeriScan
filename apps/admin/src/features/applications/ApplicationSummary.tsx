import type { ApplicationUsage } from '@/shared/api/types';

export function ApplicationSummary({
  activeKeyCount,
  usage,
  usageLoading,
}: {
  activeKeyCount: number;
  usage?: ApplicationUsage;
  usageLoading: boolean;
}) {
  const total = usage?.itemCount ?? 0;
  const rejectRate = total > 0 ? (usage!.rejectCount * 100) / total : null;
  const reviewRate = total > 0 ? (usage!.reviewCount * 100) / total : null;
  const displayValue = (value: string | null) => (usageLoading ? '读取中' : (value ?? '暂无统计'));

  return (
    <section className="detail-summary-grid">
      <div className="summary-number">
        <span>近 7 天请求</span>
        <strong>{displayValue(usage ? usage.requestCount.toLocaleString() : null)}</strong>
        <small>{usage ? `${usage.itemCount.toLocaleString()} 条内容` : '暂无数据'}</small>
      </div>
      <div className="summary-number">
        <span>不通过率</span>
        <strong className="summary-number--red">
          {displayValue(rejectRate === null ? null : `${rejectRate.toFixed(1)}%`)}
        </strong>
        <small>{rejectRate === null ? '暂无数据' : '按审核内容计算'}</small>
      </div>
      <div className="summary-number">
        <span>建议复核</span>
        <strong className="summary-number--amber">
          {displayValue(reviewRate === null ? null : `${reviewRate.toFixed(1)}%`)}
        </strong>
        <small>{reviewRate === null ? '暂无数据' : '由调用方处理'}</small>
      </div>
      <div className="summary-number">
        <span>当前 API Key</span>
        <strong>{activeKeyCount}</strong>
        <small>枚有效凭证</small>
      </div>
    </section>
  );
}
