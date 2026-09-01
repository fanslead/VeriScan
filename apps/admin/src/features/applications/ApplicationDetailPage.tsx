import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Skeleton, Toast } from '@douyinfe/semi-ui';
import { useNavigate, useParams } from 'react-router-dom';
import { apiMode, moderationService } from '@/shared/api/services';
import { ErrorState } from '@/shared/ui/ErrorState';
import { ApplicationCredentialsCard } from './ApplicationCredentialsCard';
import { ApplicationDecisionCard } from './ApplicationDecisionCard';
import { ApplicationDetailHeader } from './ApplicationDetailHeader';
import { ApplicationPolicyCard } from './ApplicationPolicyCard';
import { ApplicationSummary } from './ApplicationSummary';
import { ApplicationUsageCard } from './ApplicationUsageCard';

export function ApplicationDetailPage() {
  const { appId = '' } = useParams();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const application = useQuery({
    queryKey: ['application', appId],
    queryFn: () => moderationService.getApplication(appId),
    enabled: Boolean(appId),
  });
  const keys = useQuery({
    queryKey: ['application-keys', appId],
    queryFn: () => moderationService.listKeys(appId),
    enabled: Boolean(appId),
  });
  const toggleMutation = useMutation({
    mutationFn: (nextStatus: 'active' | 'paused') =>
      moderationService.setApplicationStatus(appId, nextStatus),
    onSuccess: (updated) => {
      queryClient.setQueryData(['application', appId], updated);
      void queryClient.invalidateQueries({ queryKey: ['applications'] });
      Toast.success({ content: updated.status === 'active' ? '应用已启用' : '应用已暂停' });
    },
    onError: () => Toast.error({ content: '应用状态更新失败，请重试' }),
  });

  if (application.isPending)
    return (
      <div className="page-stack">
        <Skeleton.Title style={{ width: 320 }} />
        <Skeleton.Paragraph rows={8} />
      </div>
    );
  if (application.isError)
    return (
      <div className="page-stack">
        <ErrorState title="应用暂时无法打开" onRetry={() => application.refetch()} />
      </div>
    );

  const app = application.data;
  const activeKeys = keys.data?.filter((key) => key.status === 'active') ?? [];
  const hasUsage = app.totalRequests !== null && app.rejectRate !== null && app.reviewRate !== null;
  const passRate = hasUsage
    ? Math.max(0, 100 - (app.rejectRate ?? 0) - (app.reviewRate ?? 0))
    : null;
  const rail = [
    {
      label: '规则筛查',
      value: apiMode === 'mock' ? '98.3%' : '暂无统计',
      tone: 'teal' as const,
      detail: apiMode === 'mock' ? '快速完成' : '暂无数据',
    },
    {
      label: '语义判断',
      value: app.reviewRate === null ? '暂无统计' : `${app.reviewRate.toFixed(1)}%`,
      tone: 'amber' as const,
      detail: app.reviewRate === null ? '暂无数据' : '边界内容',
    },
    {
      label: '最终结论',
      value: apiMode === 'mock' ? '100%' : '暂无统计',
      tone: 'teal' as const,
      detail: apiMode === 'mock' ? '机器已完成' : '暂无数据',
    },
    {
      label: '建议复核',
      value: app.reviewRate === null ? '暂无统计' : `${app.reviewRate.toFixed(1)}%`,
      tone: 'amber' as const,
      detail: app.reviewRate === null ? '暂无数据' : '由调用方处理',
    },
  ];

  return (
    <div className="page-stack application-detail-page">
      <ApplicationDetailHeader
        application={app}
        loading={toggleMutation.isPending}
        onBack={() => navigate('/applications')}
        onToggle={() => toggleMutation.mutate(app.status === 'active' ? 'paused' : 'active')}
      />
      <ApplicationSummary application={app} activeKeyCount={activeKeys.length} />
      <section className="detail-grid">
        <ApplicationDecisionCard nodes={rail} policyVersion={app.policyVersion} />
        <ApplicationCredentialsCard
          applicationId={app.id}
          activeKeys={activeKeys}
          loading={keys.isPending}
          onCreate={() => navigate(`/applications/${app.id}/keys?create=1`)}
        />
      </section>
      <section className="detail-grid detail-grid--lower">
        <ApplicationPolicyCard
          application={app}
          isMock={apiMode === 'mock'}
          onOpenRules={() => navigate('/rules')}
        />
        <ApplicationUsageCard
          application={app}
          isMock={apiMode === 'mock'}
          hasUsage={hasUsage}
          passRate={passRate}
        />
      </section>
    </div>
  );
}
