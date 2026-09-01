import { useQuery } from '@tanstack/react-query';
import { Button, Card, Skeleton, Tag, Toast, Typography } from '@douyinfe/semi-ui';
import { IconArrowRight, IconDownload, IconPlus } from '@douyinfe/semi-icons';
import { Link, useNavigate } from 'react-router-dom';
import { apiMode, moderationService } from '@/shared/api/services';
import { DecisionRail } from '@/shared/ui/DecisionRail';
import { ErrorState } from '@/shared/ui/ErrorState';
import { Metric } from './Metric';
import { RecentRecord } from './RecentRecord';
import { SignalsPanel } from './SignalsPanel';
import { TrendChart } from './TrendChart';

export function DashboardPage() {
  const navigate = useNavigate();
  const overview = useQuery({ queryKey: ['overview'], queryFn: moderationService.getOverview });

  if (overview.isPending) {
    return (
      <div className="page-stack">
        <div className="page-intro">
          <Skeleton.Title style={{ width: 260 }} />
          <Skeleton.Paragraph rows={1} />
        </div>
        <div className="dashboard-grid">
          <Card>
            <Skeleton.Paragraph rows={6} />
          </Card>
          <Card>
            <Skeleton.Paragraph rows={6} />
          </Card>
        </div>
      </div>
    );
  }

  if (overview.isError) {
    return (
      <div className="page-stack">
        <ErrorState onRetry={() => overview.refetch()} />
      </div>
    );
  }

  const stats = overview.data;
  const todayLabel = new Intl.DateTimeFormat('zh-CN', {
    year: 'numeric',
    month: '2-digit',
    day: '2-digit',
  }).format(new Date());
  return (
    <div className="page-stack dashboard-page">
      <div className="page-intro dashboard-intro">
        <div>
          <div className="eyebrow">CALIBRATION DESK / {todayLabel}</div>
          <Typography.Title heading={1}>风险态势</Typography.Title>
          <Typography.Text type="tertiary">
            今天的系统判定是否仍然可靠？从这里开始定位。
          </Typography.Text>
        </div>
        <div className="page-actions">
          {apiMode === 'mock' ? (
            <Button
              icon={<IconDownload />}
              onClick={() => Toast.info({ content: '导出任务已创建，稍后可在通知中查看' })}
            >
              导出今日记录
            </Button>
          ) : null}
          <Button
            theme="solid"
            type="primary"
            icon={<IconPlus />}
            onClick={() => navigate('/applications/new')}
          >
            新建应用
          </Button>
        </div>
      </div>

      <section className="metric-strip" aria-label="今日核心指标">
        <Metric
          label="今日审核量"
          value={stats.todayRequests === null ? '暂无统计' : stats.todayRequests.toLocaleString()}
          delta={stats.requestDelta}
          tone="teal"
        />
        <Metric
          label="不通过率"
          value={stats.rejectRate === null ? '暂无统计' : stats.rejectRate.toFixed(1)}
          unit="%"
          delta={stats.rejectDelta}
          tone="red"
        />
        <Metric
          label="建议复核"
          value={stats.reviewRate === null ? '暂无统计' : stats.reviewRate.toFixed(1)}
          unit="%"
          delta={stats.reviewDelta}
          tone="amber"
        />
        <Metric
          label="P95 处理时延"
          value={stats.p95LatencyMs === null ? '暂无统计' : String(stats.p95LatencyMs)}
          unit="ms"
          delta={stats.latencyDelta}
          tone="neutral"
        />
      </section>

      <section className="dashboard-grid dashboard-grid--main">
        <Card
          className="panel panel--trend"
          title={
            <div className="panel-heading">
              <div>
                <span className="section-kicker">FLOW / TODAY</span>
                <h2>审核流量</h2>
              </div>
              {apiMode === 'mock' ? <Tag color="green">实时</Tag> : null}
            </div>
          }
        >
          {stats.trend.length > 0 ? (
            <TrendChart data={stats.trend} />
          ) : (
            <div className="mini-empty">暂无趋势数据</div>
          )}
        </Card>
        <Card
          className="panel panel--rail"
          title={
            <div className="panel-heading">
              <div>
                <span className="section-kicker">DECISION TRACE</span>
                <h2>判定轨</h2>
              </div>
              {apiMode === 'mock' ? <span className="panel-meta">过去 24 小时</span> : null}
            </div>
          }
        >
          {stats.decisionRail.length > 0 ? (
            <>
              <DecisionRail nodes={stats.decisionRail} />
              <div className="rail-note">
                <span className="rail-note__mark" aria-hidden="true" />
                建议复核代表机器结果存在边界，不是系统故障。调用方可根据自身流程处理。
              </div>
            </>
          ) : (
            <div className="mini-empty">暂无判定轨数据</div>
          )}
        </Card>
      </section>

      <section className="dashboard-grid dashboard-grid--lower">
        {apiMode === 'mock' ? <SignalsPanel onRefresh={() => void overview.refetch()} /> : null}
        <Card
          className="panel panel--recent"
          title={
            <div className="panel-heading">
              <div>
                <span className="section-kicker">LATEST / RECORDS</span>
                <h2>最近判定</h2>
              </div>
              <Link to="/records" className="panel-link">
                全部记录 <IconArrowRight />
              </Link>
            </div>
          }
        >
          <div className="recent-records">
            {stats.recentRecords.length > 0 ? (
              stats.recentRecords.map((record) => <RecentRecord key={record.id} record={record} />)
            ) : (
              <div className="mini-empty">暂无最近记录</div>
            )}
          </div>
        </Card>
      </section>
    </div>
  );
}
