import { useEffect, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Input, Select, Skeleton, Table, Typography } from '@douyinfe/semi-ui';
import { IconArrowRight, IconSearch } from '@douyinfe/semi-icons';
import { useNavigate, useParams, useSearchParams } from 'react-router-dom';
import type { Application, ModerationStatus } from '@/shared/api/types';
import { moderationService } from '@/shared/api/services';
import { EmptyState } from '@/shared/ui/EmptyState';
import { ErrorState } from '@/shared/ui/ErrorState';
import { PageIntro } from '@/shared/ui/PageIntro';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { formatDate } from '@/shared/ui/formatDate';
import { RecordDetailDrawer } from './RecordDetailDrawer';

export function ModerationRecordsPage() {
  const { recordId } = useParams();
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [keyword, setKeyword] = useState(searchParams.get('keyword') ?? '');
  const status = (searchParams.get('status') as ModerationStatus | 'all' | null) ?? 'all';
  const applicationId = searchParams.get('applicationId') ?? undefined;
  const applications = useQuery({
    queryKey: ['applications', 'record-filter'],
    queryFn: () => moderationService.listApplications(),
  });
  const records = useQuery({
    queryKey: ['records', keyword, status, applicationId],
    queryFn: () =>
      moderationService.listRecords({ keyword, status, applicationId, page: 1, pageSize: 8 }),
  });
  const detail = useQuery({
    queryKey: ['record', recordId],
    queryFn: () => moderationService.getRecord(recordId ?? ''),
    enabled: Boolean(recordId),
  });

  useEffect(() => {
    if ((searchParams.get('keyword') ?? '') === keyword) return;
    const timer = window.setTimeout(() => {
      const next = new URLSearchParams(searchParams);
      if (keyword) next.set('keyword', keyword);
      else next.delete('keyword');
      setSearchParams(next, { replace: true });
    }, 250);
    return () => window.clearTimeout(timer);
  }, [keyword, searchParams, setSearchParams]);

  const setFilter = (key: string, value: string) => {
    const next = new URLSearchParams(searchParams);
    if (!value || value === 'all') next.delete(key);
    else next.set(key, value);
    setSearchParams(next);
  };
  const openDetail = (id: string) =>
    navigate(`/records/${id}${searchParams.toString() ? `?${searchParams.toString()}` : ''}`);
  const closeDetail = () =>
    navigate(`/records${searchParams.toString() ? `?${searchParams.toString()}` : ''}`);
  const columns = [
    {
      title: '记录',
      dataIndex: 'id',
      width: 140,
      render: (value: string) => <span className="record-id">{value}</span>,
    },
    {
      title: '内容',
      dataIndex: 'contentPreview',
      width: 420,
      render: (value: string) => <span className="record-content">{value}</span>,
    },
    {
      title: '应用',
      dataIndex: 'applicationName',
      render: (value: string) => (
        <span className="table-secondary">
          <strong>{value}</strong>
          <small>调用方应用</small>
        </span>
      ),
    },
    {
      title: '结论',
      dataIndex: 'status',
      width: 120,
      render: (value: ModerationStatus) => <StatusBadge status={value} compact />,
    },
    {
      title: '置信度',
      dataIndex: 'confidence',
      width: 100,
      align: 'right' as const,
      render: (value: number | null) => (
        <span className="data-mono">
          {value === null ? '暂无数据' : `${(value * 100).toFixed(0)}%`}
        </span>
      ),
    },
    {
      title: '时间',
      dataIndex: 'createdAt',
      width: 150,
      render: (value: string) => (
        <span className="table-secondary">
          <strong>{formatDate(value, { month: 'numeric', day: 'numeric' })}</strong>
          <small>{formatDate(value, { hour: '2-digit', minute: '2-digit' })}</small>
        </span>
      ),
    },
    {
      title: '',
      dataIndex: 'action',
      width: 48,
      render: (_: unknown, record: { id: string }) => (
        <Button
          theme="borderless"
          icon={<IconArrowRight />}
          aria-label={`查看 ${record.id}`}
          onClick={() => openDetail(record.id)}
        />
      ),
    },
  ];

  return (
    <div className="page-stack records-page">
      <PageIntro
        eyebrow="结果查询 · 机器判定"
        title="审核记录"
        description="查看每一次机器判定的结果、依据与处理时延。这里不生成人工复核任务。"
      />
      <Card className="panel table-panel">
        <div className="table-toolbar">
          <div>
            <span className="section-kicker">全部结果记录</span>
            <Typography.Title heading={4}>全部记录</Typography.Title>
          </div>
          <div className="table-filters">
            <Input
              prefix={<IconSearch />}
              value={keyword}
              onChange={setKeyword}
              placeholder="搜索业务 ID 或内容摘要"
              showClear
              aria-label="搜索审核记录"
            />
            <Select
              value={applicationId ?? 'all'}
              onChange={(value) => setFilter('applicationId', String(value))}
              optionList={[
                { value: 'all', label: '全部应用' },
                ...(applications.data?.items ?? []).map((app: Application) => ({
                  value: app.id,
                  label: app.name,
                })),
              ]}
              aria-label="按应用筛选"
            />
            <Select
              value={status}
              onChange={(value) => setFilter('status', String(value))}
              optionList={[
                { value: 'all', label: '全部结论' },
                { value: 'pass', label: '通过' },
                { value: 'reject', label: '不通过' },
                { value: 'review', label: '建议复核' },
              ]}
              aria-label="按结论筛选"
            />
          </div>
        </div>
        {records.isPending ? (
          <div className="table-skeleton">
            <Skeleton.Paragraph rows={7} />
          </div>
        ) : null}
        {records.isError ? <ErrorState onRetry={() => records.refetch()} /> : null}
        {records.isSuccess && records.data.items.length === 0 ? (
          <EmptyState
            title="还没有符合条件的记录"
            description="调整筛选条件后再试，新的审核结果会自动出现在这里。"
          />
        ) : null}
        {records.isSuccess && records.data.items.length > 0 ? (
          <Table
            className="data-table"
            columns={columns}
            dataSource={records.data.items}
            rowKey="id"
            pagination={false}
            onRow={(record) => ({
              onClick: () => {
                if (record) openDetail(record.id);
              },
              className: 'clickable-row',
            })}
          />
        ) : null}
      </Card>
      <div className="records-summary">
        <span>结果只读保存</span>
        <span>共 {records.data?.total ?? '—'} 条</span>
        <span>数据按应用隔离</span>
      </div>
      <RecordDetailDrawer
        visible={Boolean(recordId)}
        loading={detail.isPending}
        error={detail.isError}
        record={detail.data}
        onRetry={() => {
          void detail.refetch();
        }}
        onClose={closeDetail}
      />
    </div>
  );
}
