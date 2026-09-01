import { useEffect, useMemo, useState } from 'react';
import { useQuery } from '@tanstack/react-query';
import { Button, Card, Input, Select, Skeleton, Table, Tag, Typography } from '@douyinfe/semi-ui';
import { IconArrowRight, IconPlus, IconSearch } from '@douyinfe/semi-icons';
import { Link, useNavigate, useSearchParams } from 'react-router-dom';
import type { Application, ApplicationStatus } from '@/shared/api/types';
import { moderationService } from '@/shared/api/services';
import { EmptyState } from '@/shared/ui/EmptyState';
import { ErrorState } from '@/shared/ui/ErrorState';
import { PageIntro } from '@/shared/ui/PageIntro';
import { StatusBadge } from '@/shared/ui/StatusBadge';
import { useAdminCapability } from '@/shared/auth/permissions';

export function ApplicationListPage() {
  const canOperate = useAdminCapability('operate');
  const navigate = useNavigate();
  const [searchParams, setSearchParams] = useSearchParams();
  const [keyword, setKeyword] = useState(searchParams.get('keyword') ?? '');
  const status = (searchParams.get('status') as ApplicationStatus | 'all' | null) ?? 'all';
  const query = useQuery({
    queryKey: ['applications', keyword, status],
    queryFn: () => moderationService.listApplications({ keyword, status }),
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

  const columns = useMemo(
    () => [
      {
        title: '应用',
        dataIndex: 'name',
        width: 280,
        render: (_: unknown, record: Application) => (
          <Link to={`/applications/${record.id}`} className="table-primary-link">
            <span className="app-avatar">{record.name.slice(0, 1)}</span>
            <span>
              <strong>{record.name}</strong>
              <small>
                {record.slug} ·{' '}
                {record.environment === 'live'
                  ? '正式'
                  : record.environment === 'test'
                    ? '测试'
                    : '暂无环境'}
              </small>
            </span>
            <IconArrowRight />
          </Link>
        ),
      },
      {
        title: '状态',
        dataIndex: 'status',
        width: 120,
        render: (value: ApplicationStatus) => <StatusBadge status={value} compact />,
      },
      {
        title: '策略',
        dataIndex: 'policyName',
        render: (_: unknown, record: Application) => (
          <span className="table-secondary">
            <strong>{record.policyName || '暂无策略'}</strong>
            <small>{record.policyVersion ? `版本 ${record.policyVersion}` : '暂无版本'}</small>
          </span>
        ),
      },
      {
        title: '今日请求',
        dataIndex: 'totalRequests',
        align: 'right' as const,
        render: (value: number | null) => (
          <span className="data-mono">{value === null ? '暂无统计' : value.toLocaleString()}</span>
        ),
      },
      {
        title: '建议复核',
        dataIndex: 'reviewRate',
        align: 'right' as const,
        render: (value: number | null) => (
          <span className="data-mono data-mono--amber">
            {value === null ? '暂无统计' : `${value.toFixed(1)}%`}
          </span>
        ),
      },
      {
        title: 'API Key',
        dataIndex: 'activeKeyCount',
        align: 'right' as const,
        render: (value: number) => <span className="data-mono">{value} 枚</span>,
      },
    ],
    [],
  );

  const setStatus = (value: ApplicationStatus | 'all') => {
    const next = new URLSearchParams(searchParams);
    if (value === 'all') next.delete('status');
    else next.set('status', value);
    setSearchParams(next);
  };

  return (
    <div className="page-stack">
      <PageIntro
        eyebrow="接入管理 · 应用目录"
        title="应用"
        description="按应用查看调用规模、策略状态与凭证健康度。"
        actions={
          canOperate ? (
            <Button
              theme="solid"
              type="primary"
              icon={<IconPlus />}
              onClick={() => navigate('/applications/new')}
            >
              创建应用
            </Button>
          ) : undefined
        }
      />
      <Card className="panel table-panel">
        <div className="table-toolbar">
          <div>
            <span className="section-kicker">
              {query.data ? `${query.data.total} 个应用` : '应用目录'}
            </span>
            <Typography.Title heading={4}>全部应用</Typography.Title>
          </div>
          <div className="table-filters">
            <Input
              prefix={<IconSearch />}
              value={keyword}
              onChange={setKeyword}
              placeholder="搜索应用名称或标识"
              showClear
              aria-label="搜索应用"
            />
            <Select
              value={status}
              onChange={(value) => setStatus(value as ApplicationStatus | 'all')}
              optionList={[
                { value: 'all', label: '全部状态' },
                { value: 'active', label: '运行中' },
                { value: 'paused', label: '已暂停' },
              ]}
              aria-label="按状态筛选"
            />
          </div>
        </div>
        {query.isPending ? (
          <div className="table-skeleton">
            <Skeleton.Paragraph rows={5} />
          </div>
        ) : null}
        {query.isError ? <ErrorState onRetry={() => query.refetch()} /> : null}
        {query.isSuccess && query.data.items.length === 0 ? (
          <EmptyState
            title="还没有匹配的应用"
            description="换个关键词，或先创建一个新的应用。"
            actionText={canOperate ? '创建应用' : undefined}
            onAction={canOperate ? () => navigate('/applications/new') : undefined}
          />
        ) : null}
        {query.isSuccess && query.data.items.length > 0 ? (
          <Table
            className="data-table"
            columns={columns}
            dataSource={query.data.items}
            rowKey="id"
            pagination={false}
          />
        ) : null}
      </Card>
      <div className="page-footnote">
        <Tag color="grey">提示</Tag>
        <span>应用是统计、配额与 API Key 管理的边界；删除应用前请先停用全部凭证。</span>
      </div>
    </div>
  );
}
