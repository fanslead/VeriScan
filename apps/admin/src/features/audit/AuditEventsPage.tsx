import { useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Select, SideSheet, Skeleton, Table, Tag } from '@douyinfe/semi-ui';
import { IconEyeOpened, IconHistory } from '@douyinfe/semi-icons';
import { auditService, moderationService } from '@/shared/api/services';
import type { AuditEvent } from '@/shared/api/types';
import { ErrorState } from '@/shared/ui/ErrorState';
import { PageIntro } from '@/shared/ui/PageIntro';
import { formatDate } from '@/shared/ui/formatDate';
import {
  auditActionOptions,
  getAuditActionLabel,
  getAuditChanges,
  getAuditResourceLabel,
} from './auditEventModel';

export function AuditEventsPage() {
  const [applicationId, setApplicationId] = useState('');
  const [action, setAction] = useState('');
  const [selected, setSelected] = useState<AuditEvent | null>(null);
  const events = useQuery({
    queryKey: ['audit-events', applicationId, action],
    queryFn: () => auditService.list({ applicationId, action, limit: 200 }),
  });
  const applications = useQuery({
    queryKey: ['applications', 'audit-filter'],
    queryFn: () => moderationService.listApplications(),
  });
  const changes = useMemo(() => (selected ? getAuditChanges(selected) : []), [selected]);

  const columns = [
    {
      title: '发生时间',
      dataIndex: 'occurredAt',
      width: 176,
      render: (value: string) => (
        <span className="audit-time">
          <strong>{formatDate(value, { month: '2-digit', day: '2-digit' })}</strong>
          <small>
            {formatDate(value, { hour: '2-digit', minute: '2-digit', second: '2-digit' })}
          </small>
        </span>
      ),
    },
    {
      title: '操作记录',
      dataIndex: 'action',
      render: (_: unknown, event: AuditEvent) => (
        <span className="audit-event-name">
          <strong>{getAuditActionLabel(event.action)}</strong>
          <small>{getAuditResourceLabel(event.resourceType)}</small>
        </span>
      ),
    },
    {
      title: '操作人',
      dataIndex: 'actorId',
      width: 160,
      render: (value: string | null) => value || '工作区管理员',
    },
    {
      title: '变更项',
      dataIndex: 'beforeJson',
      width: 110,
      render: (_: unknown, event: AuditEvent) => {
        const count = getAuditChanges(event).length;
        return <Tag color={count ? 'cyan' : 'grey'}>{count ? `${count} 项` : '已记录'}</Tag>;
      },
    },
    {
      title: '操作',
      dataIndex: 'id',
      width: 110,
      align: 'right' as const,
      render: (_: unknown, event: AuditEvent) => (
        <Button
          theme="borderless"
          type="tertiary"
          icon={<IconEyeOpened />}
          onClick={() => setSelected(event)}
        >
          查看
        </Button>
      ),
    },
  ];

  return (
    <div className="page-stack audit-page">
      <PageIntro
        eyebrow="安全治理 · 操作记录"
        title="审计日志"
        description="查看应用、密钥、规则和 AI 配置的关键变更。"
      />
      <Card className="panel table-panel">
        <div className="table-toolbar">
          <div>
            <span className="section-kicker">关键操作留痕</span>
            <h2>最近 90 天</h2>
          </div>
          <div className="table-filters">
            <Select
              value={applicationId}
              optionList={[
                { value: '', label: '全部应用' },
                ...(applications.data?.items.map((app) => ({ value: app.id, label: app.name })) ??
                  []),
              ]}
              onChange={(value) => setApplicationId(String(value))}
              aria-label="按应用筛选审计日志"
            />
            <Select
              value={action}
              optionList={[{ value: '', label: '全部操作' }, ...auditActionOptions]}
              onChange={(value) => setAction(String(value))}
              aria-label="按操作筛选审计日志"
            />
          </div>
        </div>
        {events.isPending ? (
          <div className="table-skeleton">
            <Skeleton.Paragraph rows={6} />
          </div>
        ) : events.isError ? (
          <ErrorState title="审计日志暂时无法读取" onRetry={() => void events.refetch()} />
        ) : (
          <Table
            className="data-table"
            columns={columns}
            dataSource={events.data.items}
            rowKey="id"
            pagination={false}
            empty="当前筛选条件下没有操作记录"
          />
        )}
      </Card>

      <SideSheet
        visible={Boolean(selected)}
        title="操作详情"
        width={520}
        onCancel={() => setSelected(null)}
        footer={null}
      >
        {selected ? (
          <div className="audit-detail">
            <div className="audit-detail__lead">
              <span className="audit-detail__icon">
                <IconHistory />
              </span>
              <div>
                <span className="section-kicker">
                  {getAuditResourceLabel(selected.resourceType)}
                </span>
                <h2>{getAuditActionLabel(selected.action)}</h2>
                <p>{formatDate(selected.occurredAt)}</p>
              </div>
            </div>
            <dl className="audit-detail__facts">
              <div>
                <dt>操作人</dt>
                <dd>{selected.actorId || '工作区管理员'}</dd>
              </div>
              <div>
                <dt>对象标识</dt>
                <dd>{selected.resourceId}</dd>
              </div>
              {selected.applicationId ? (
                <div>
                  <dt>所属应用</dt>
                  <dd>
                    {applications.data?.items.find((app) => app.id === selected.applicationId)
                      ?.name ?? selected.applicationId}
                  </dd>
                </div>
              ) : null}
            </dl>
            <section className="audit-detail__changes">
              <h3>变更内容</h3>
              {changes.length ? (
                changes.map((change) => (
                  <div className="audit-change" key={change.label}>
                    <strong>{change.label}</strong>
                    <span>
                      <small>变更前</small>
                      {change.before}
                    </span>
                    <i aria-hidden="true">→</i>
                    <span>
                      <small>变更后</small>
                      {change.after}
                    </span>
                  </div>
                ))
              ) : (
                <p>本次操作已完成安全留痕，没有可展示的字段差异。</p>
              )}
            </section>
          </div>
        ) : null}
      </SideSheet>
    </div>
  );
}
