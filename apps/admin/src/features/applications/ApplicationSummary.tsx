import type { Application } from '@/shared/api/types';

export function ApplicationSummary({
  application,
  activeKeyCount,
}: {
  application: Application;
  activeKeyCount: number;
}) {
  return (
    <section className="detail-summary-grid">
      <div className="summary-number">
        <span>累计审核量</span>
        <strong>
          {application.totalRequests === null
            ? '暂无统计'
            : application.totalRequests.toLocaleString()}
        </strong>
        <small>{application.totalRequests === null ? '暂无数据' : '自创建以来'}</small>
      </div>
      <div className="summary-number">
        <span>不通过率</span>
        <strong className="summary-number--red">
          {application.rejectRate === null ? '暂无统计' : `${application.rejectRate.toFixed(1)}%`}
        </strong>
        <small>{application.rejectRate === null ? '暂无数据' : '近 24 小时'}</small>
      </div>
      <div className="summary-number">
        <span>建议复核</span>
        <strong className="summary-number--amber">
          {application.reviewRate === null ? '暂无统计' : `${application.reviewRate.toFixed(1)}%`}
        </strong>
        <small>{application.reviewRate === null ? '暂无数据' : '近 24 小时'}</small>
      </div>
      <div className="summary-number">
        <span>当前 API Key</span>
        <strong>{activeKeyCount}</strong>
        <small>枚有效凭证</small>
      </div>
    </section>
  );
}
