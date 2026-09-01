import { SideSheet, Skeleton, Tag, Typography } from '@douyinfe/semi-ui';
import type { ModerationRecord } from '@/shared/api/types';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { reviewSourceLabel } from '@/shared/ui/statusText';
import { DecisionRail } from '@/shared/ui/DecisionRail';
import { ErrorState } from '@/shared/ui/ErrorState';

interface RecordDetailDrawerProps {
  visible: boolean;
  record: ModerationRecord | undefined;
  loading?: boolean;
  error?: boolean;
  onRetry?: () => void;
  onClose: () => void;
}

export function RecordDetailDrawer({
  visible,
  record,
  loading = false,
  error = false,
  onRetry,
  onClose,
}: RecordDetailDrawerProps) {
  return (
    <SideSheet
      visible={visible}
      title="审核记录详情"
      placement="right"
      width={560}
      onCancel={onClose}
      maskClosable={false}
    >
      {error ? (
        <ErrorState
          title="记录暂时无法打开"
          description="请稍后重试，或者先返回记录列表。"
          onRetry={onRetry ?? onClose}
        />
      ) : loading || !record ? (
        <div className="drawer-loading">
          <Skeleton.Paragraph rows={8} />
        </div>
      ) : (
        <div className="record-drawer">
          <div className="record-drawer__header">
            <div>
              <span className="section-kicker">RECORD / {record.id}</span>
              <Typography.Title heading={4}>{record.applicationName}</Typography.Title>
            </div>
            <StatusBadge status={record.status} />
          </div>
          <div className="record-meta-grid">
            <div>
              <span>结论置信度</span>
              <strong>
                {record.confidence === null
                  ? '暂无数据'
                  : `${(record.confidence * 100).toFixed(0)}%`}
              </strong>
            </div>
            <div>
              <span>处理时延</span>
              <strong>{record.latencyMs === null ? '暂无数据' : `${record.latencyMs}ms`}</strong>
            </div>
            <div>
              <span>检测级别</span>
              <strong>{record.detectLevel ? `第 ${record.detectLevel} 级` : '暂无数据'}</strong>
            </div>
            <div>
              <span>策略版本</span>
              <strong>{record.policyVersion || '暂无数据'}</strong>
            </div>
          </div>
          <div className="drawer-section">
            <div className="drawer-label">内容</div>
            <div className="content-quote">{record.contentPreview || '暂无内容'}</div>
            <div className="content-hash">内容指纹 {record.contentHash || '暂无数据'}</div>
          </div>
          <div className="drawer-section">
            <div className="drawer-label">机器判定</div>
            <div className="record-verdict-row">
              <StatusBadge status={record.status} />
              <div>
                <strong>{record.reason}</strong>
                {record.reviewSource ? <span>{reviewSourceLabel(record.reviewSource)}</span> : null}
              </div>
            </div>
          </div>
          <div className="drawer-section">
            <div className="drawer-label">判定轨</div>
            <DecisionRail
              compact
              nodes={[
                {
                  label: '规则筛查',
                  value:
                    record.detectLevel === null
                      ? '暂无数据'
                      : record.detectLevel === 1
                        ? '命中'
                        : '未调用',
                  tone: record.detectLevel === 1 ? 'teal' : 'neutral',
                  detail: record.detectLevel === null ? '暂无数据' : '快速检测',
                },
                {
                  label: '语义判断',
                  value:
                    record.detectLevel === null
                      ? '暂无数据'
                      : record.detectLevel === 2
                        ? '已调用'
                        : '未调用',
                  tone: record.detectLevel === 2 ? 'amber' : 'neutral',
                  detail:
                    record.detectLevel === null
                      ? '暂无数据'
                      : record.detectLevel === 2
                        ? '外部模型'
                        : '规则明确',
                },
                {
                  label: '最终结论',
                  value:
                    record.status === 'pass'
                      ? '通过'
                      : record.status === 'reject'
                        ? '不通过'
                        : '建议复核',
                  tone:
                    record.status === 'pass'
                      ? 'teal'
                      : record.status === 'reject'
                        ? 'red'
                        : 'amber',
                  detail: '已完成',
                },
              ]}
            />
          </div>
          <div className="drawer-section">
            <div className="drawer-label">证据片段</div>
            <div className="evidence-list">
              {record.evidence.map((evidence) => (
                <div key={evidence}>
                  <span aria-hidden="true">✓</span>
                  {evidence}
                </div>
              ))}
            </div>
          </div>
          {record.status === 'review' ? (
            <div className="review-note">
              <Tag color="amber">建议复核</Tag>
              <p>该结果已返回调用方，本系统只保留机器结论，不会在后台生成复核任务。</p>
            </div>
          ) : null}
        </div>
      )}
    </SideSheet>
  );
}
