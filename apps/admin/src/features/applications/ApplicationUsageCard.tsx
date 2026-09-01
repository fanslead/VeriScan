import { Card } from '@douyinfe/semi-ui';
import { IconArrowRight } from '@douyinfe/semi-icons';
import { Link } from 'react-router-dom';
import type { Application } from '@/shared/api/types';

export function ApplicationUsageCard({
  application,
  isMock,
  hasUsage,
  passRate,
}: {
  application: Application;
  isMock: boolean;
  hasUsage: boolean;
  passRate: number | null;
}) {
  return (
    <Card
      className="panel detail-panel"
      title={
        <div className="panel-heading">
          <div>
            <span className="section-kicker">{isMock ? 'USAGE / 24H' : 'USAGE / RECENT'}</span>
            <h2>调用概览</h2>
          </div>
          <Link to={`/records?applicationId=${application.id}`} className="panel-link">
            查看记录 <IconArrowRight />
          </Link>
        </div>
      }
    >
      {hasUsage ? (
        <>
          <div className="usage-bars">
            <div className="usage-bar">
              <span>通过</span>
              <div>
                <i style={{ width: `${passRate}%` }} />
              </div>
              <strong>{passRate?.toFixed(1)}%</strong>
            </div>
            <div className="usage-bar usage-bar--red">
              <span>不通过</span>
              <div>
                <i style={{ width: `${Math.max(application.rejectRate ?? 0, 1)}%` }} />
              </div>
              <strong>{application.rejectRate?.toFixed(1)}%</strong>
            </div>
            <div className="usage-bar usage-bar--amber">
              <span>建议复核</span>
              <div>
                <i style={{ width: `${Math.max(application.reviewRate ?? 0, 1)}%` }} />
              </div>
              <strong>{application.reviewRate?.toFixed(1)}%</strong>
            </div>
          </div>
          {isMock ? <div className="usage-footnote">数据每 5 分钟更新一次</div> : null}
        </>
      ) : (
        <div className="mini-empty">暂无统计数据</div>
      )}
    </Card>
  );
}
